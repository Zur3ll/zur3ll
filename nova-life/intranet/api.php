<?php
/*
 * Intranet Neoran : API du serveur, version PHP (hébergement Web Infomaniak).
 * Appelée via .htaccess : /api/<route> -> api.php?route=<route>.
 * Comptes : joueurs, staff et admins dans data/staff.json. Entreprises : data/entreprises.json,
 * tchats : data/tchat-<id>.json.
 * Configuration : fichier .env à côté de ce fichier (ADMIN_MDP, ADMIN_IDENTIFIANT).
 * Données : dossier data/ (protégé par .htaccess).
 */

ini_set('display_errors', '0');
error_reporting(E_ALL);

const DUREE_SESSION = 7 * 24 * 3600;
const ROLES = ['admin', 'staff', 'joueur'];
const GRADES = ['patron', 'gerant', 'employe'];
const MAX_TRANSACTIONS = 500;
const MAX_MESSAGES = 300;

// Sections modifiables et champs autorisés pour chaque élément (le reste est ignoré)
const SECTIONS = [
    'general'     => ['objet' => true, 'champs' => ['serveur', 'ville', 'discord']],
    'actualites'  => ['champs' => ['titre', 'date', 'auteur', 'texte']],
    'evenements'  => ['champs' => ['titre', 'date', 'heure', 'lieu']],
    'entreprises' => ['champs' => ['nom', 'secteur', 'statut', 'patron', 'tel', 'description']],
    'emplois'     => ['champs' => ['poste', 'entreprise', 'salaire', 'details']],
    'annuaire'    => ['champs' => ['nom', 'role', 'entreprise', 'tel']],
    'reglement'   => ['champs' => ['titre', 'regles']],
    'liens'       => ['champs' => ['nom', 'url', 'description']],
];

class ApiErreur extends Exception
{
    public int $statut;

    public function __construct(string $message, int $statut = 400)
    {
        parent::__construct($message);
        $this->statut = $statut;
    }
}

// ---------- Réponses ----------

function repondre(int $code, $donnees): void
{
    http_response_code($code);
    header('Content-Type: application/json; charset=utf-8');
    header('Cache-Control: no-store');
    header('X-Content-Type-Options: nosniff');
    echo json_encode($donnees, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

function journal(string $message): void
{
    error_log('[intranet] ' . $message);
}

// ---------- Configuration (.env) ----------

function chargerEnv(): array
{
    $valeurs = [];
    $fichier = __DIR__ . '/.env';
    if (!is_file($fichier)) {
        return $valeurs;
    }
    foreach (preg_split('/\r?\n/', (string) file_get_contents($fichier)) as $ligne) {
        if (!preg_match('/^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$/', $ligne, $m)) {
            continue;
        }
        $valeur = $m[2];
        if (preg_match('/^(["\']).*\1$/', $valeur)) {
            $valeur = substr($valeur, 1, -1);
        }
        $valeurs[$m[1]] = $valeur;
    }
    return $valeurs;
}

// Les variables définies par l'hébergeur sont prioritaires sur le fichier .env
function config(string $cle, string $defaut = ''): string
{
    static $env = null;
    if ($env === null) {
        $env = chargerEnv();
    }
    $valeur = getenv($cle);
    if ($valeur !== false && $valeur !== '') {
        return $valeur;
    }
    return $env[$cle] ?? $defaut;
}

// ---------- Stockage ----------

function dossierData(): string
{
    return __DIR__ . '/data';
}

function fichier(string $nom): string
{
    return dossierData() . '/' . $nom;
}

function lireJson(string $fichier, $defaut)
{
    if (!is_file($fichier)) {
        return $defaut;
    }
    $donnees = json_decode((string) file_get_contents($fichier), true);
    return $donnees === null ? $defaut : $donnees;
}

function ecrireJson(string $fichier, $valeur): void
{
    if (!is_dir(dossierData())) {
        mkdir(dossierData(), 0700, true);
    }
    $tmp = $fichier . '.tmp';
    file_put_contents($tmp, json_encode($valeur, JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES), LOCK_EX);
    rename($tmp, $fichier);
}

// Un seul écrivain à la fois : évite que deux requêtes simultanées s'écrasent
function verrouiller(): void
{
    static $verrou = null;
    if ($verrou !== null) {
        return;
    }
    if (!is_dir(dossierData())) {
        mkdir(dossierData(), 0700, true);
    }
    $verrou = fopen(fichier('verrou.lock'), 'c');
    flock($verrou, LOCK_EX);
}

function chargerStaff(): array
{
    return lireJson(fichier('staff.json'), []);
}

function sauverStaff(array $staff): void
{
    ecrireJson(fichier('staff.json'), array_values($staff));
}

// ---------- Mots de passe, comptes et sessions ----------

function hacher(string $mdp): string
{
    return password_hash($mdp, PASSWORD_DEFAULT);
}

function verifier(string $mdp, array $compte): bool
{
    return isset($compte['hash']) && password_verify($mdp, $compte['hash']);
}

function publicCompte(array $c): array
{
    return ['id' => $c['id'], 'identifiant' => $c['identifiant'], 'nom' => $c['nom'], 'role' => $c['role']];
}

// Ce que le joueur voit de son propre compte
function detailCompte(array $c): array
{
    return publicCompte($c) + [
        'telephone' => $c['telephone'] ?? '',
        'solde' => (int) ($c['solde'] ?? 0),
        'historique' => array_slice($c['historique'] ?? [], 0, 50),
        'fond' => $c['fond'] ?? null,
        'notes' => $c['notes'] ?? [],
    ];
}

function estStaff(array $c): bool
{
    return in_array($c['role'] ?? '', ['admin', 'staff'], true);
}

function uuid(): string
{
    $octets = random_bytes(16);
    $octets[6] = chr((ord($octets[6]) & 0x0f) | 0x40);
    $octets[8] = chr((ord($octets[8]) & 0x3f) | 0x80);
    return vsprintf('%s%s-%s-%s-%s-%s%s%s', str_split(bin2hex($octets), 4));
}

function indexParIdentifiant(array $staff, string $identifiant): ?int
{
    foreach ($staff as $i => $c) {
        if (strcasecmp($c['identifiant'], $identifiant) === 0) {
            return $i;
        }
    }
    return null;
}

function indexParId(array $staff, string $id): ?int
{
    foreach ($staff as $i => $c) {
        if ($c['id'] === $id) {
            return $i;
        }
    }
    return null;
}

function nombreAdmins(array $staff): int
{
    return count(array_filter($staff, fn($c) => $c['role'] === 'admin'));
}

function purgerSessions(array $sessions): array
{
    $maintenant = time();
    return array_filter($sessions, fn($s) => ($s['expire'] ?? 0) > $maintenant);
}

// Le compte admin est créé à partir de ADMIN_MDP s'il n'existe pas encore
function garantirAdmin(): void
{
    $mdp = config('ADMIN_MDP');
    if ($mdp === '') {
        if (!chargerStaff()) {
            throw new ApiErreur('Aucun compte staff. Configure ADMIN_MDP dans le fichier .env.', 500);
        }
        return;
    }
    if (mb_strlen($mdp) < 8) {
        throw new ApiErreur('ADMIN_MDP doit faire au moins 8 caractères (fichier .env).', 500);
    }
    $staff = chargerStaff();
    $identifiant = config('ADMIN_IDENTIFIANT', 'admin');
    if (indexParIdentifiant($staff, $identifiant) !== null) {
        return;
    }
    $staff[] = ['id' => uuid(), 'identifiant' => $identifiant, 'nom' => 'Administrateur', 'role' => 'admin', 'hash' => hacher($mdp)];
    sauverStaff($staff);
    journal("Compte admin « $identifiant » créé à partir du fichier .env.");
}

// Si le mot de passe saisi est celui de ADMIN_MDP, le compte admin est aligné sur le .env
function synchroniserAdmin(array &$staff, string $mdpSaisi): void
{
    $mdpEnv = config('ADMIN_MDP');
    if ($mdpEnv === '' || !hash_equals($mdpEnv, $mdpSaisi)) {
        return;
    }
    $idx = indexParIdentifiant($staff, config('ADMIN_IDENTIFIANT', 'admin'));
    if ($idx === null || verifier($mdpSaisi, $staff[$idx])) {
        return;
    }
    $staff[$idx]['hash'] = hacher($mdpSaisi);
    $staff[$idx]['role'] = 'admin';
    sauverStaff($staff);
    journal("Mot de passe du compte « {$staff[$idx]['identifiant']} » mis à jour depuis le fichier .env.");
}

function jetonBearer(): string
{
    $entete = $_SERVER['HTTP_AUTHORIZATION'] ?? $_SERVER['REDIRECT_HTTP_AUTHORIZATION'] ?? '';
    return trim((string) preg_replace('/^Bearer /', '', $entete));
}

function compteDepuisRequete(): ?array
{
    $jeton = jetonBearer();
    if ($jeton === '') {
        return null;
    }
    $sessions = purgerSessions(lireJson(fichier('sessions.json'), []));
    if (!isset($sessions[$jeton])) {
        return null;
    }
    $idx = indexParId(chargerStaff(), $sessions[$jeton]['id']);
    return $idx === null ? null : chargerStaff()[$idx];
}

// ---------- Nettoyage des données reçues ----------

function texte($valeur, int $max = 2000): string
{
    $s = is_scalar($valeur) ? (string) $valeur : '';
    return mb_substr($s, 0, $max);
}

function estListe($valeur): bool
{
    return is_array($valeur) && ($valeur === [] || array_keys($valeur) === range(0, count($valeur) - 1));
}

function nettoyerElement(string $section, $el): array
{
    $el = is_array($el) ? $el : [];
    $propre = [];
    foreach (SECTIONS[$section]['champs'] as $champ) {
        if ($champ === 'regles') {
            $regles = estListe($el['regles'] ?? null) ? $el['regles'] : [];
            $regles = array_map(fn($r) => texte($r, 500), array_slice($regles, 0, 50));
            $propre['regles'] = array_values(array_filter($regles, fn($r) => $r !== ''));
        } else {
            $propre[$champ] = texte($el[$champ] ?? null);
        }
    }
    if ($section === 'liens' && !preg_match('#^https?://#i', $propre['url'])) {
        throw new ApiErreur('Chaque lien doit commencer par http:// ou https://.');
    }
    if ($section === 'general' && $propre['discord'] !== '' && !preg_match('#^https?://#i', $propre['discord'])) {
        throw new ApiErreur('Le lien Discord doit commencer par http:// ou https://.');
    }
    return $propre;
}

function validerIdentifiant(string $identifiant): void
{
    if (!preg_match('/^[a-zA-Z0-9._-]{3,32}$/D', $identifiant)) {
        throw new ApiErreur("L'identifiant doit faire 3 à 32 caractères (lettres, chiffres, point, tiret).");
    }
}

function validerMdp(string $mdp): void
{
    if (mb_strlen($mdp) < 8) {
        throw new ApiErreur('Le mot de passe doit faire au moins 8 caractères.');
    }
}

function lireCorps(): array
{
    $brut = (string) file_get_contents('php://input');
    if (strlen($brut) > 1000000) {
        throw new ApiErreur('Données trop volumineuses.');
    }
    if (trim($brut) === '') {
        return [];
    }
    $donnees = json_decode($brut, true);
    if (!is_array($donnees)) {
        throw new ApiErreur('Données illisibles.');
    }
    return $donnees;
}

// ---------- Routes ----------

function connexion(): void
{
    $ip = $_SERVER['REMOTE_ADDR'] ?? 'inconnue';
    $maintenant = time();
    $essais = lireJson(fichier('essais.json'), []);
    $e = $essais[$ip] ?? null;
    if ($e && $e['nombre'] >= 5 && $e['jusqua'] > $maintenant) {
        throw new ApiErreur("Trop d'essais. Réessaie dans quelques minutes.", 429);
    }

    $corps = lireCorps();
    $identifiant = (string) ($corps['identifiant'] ?? '');
    $mdp = (string) ($corps['mdp'] ?? '');

    garantirAdmin();
    $staff = chargerStaff();
    synchroniserAdmin($staff, $mdp);

    $idx = indexParIdentifiant($staff, $identifiant);
    if ($idx === null || !verifier($mdp, $staff[$idx])) {
        $essais[$ip] = [
            'nombre' => ($e && $e['jusqua'] > $maintenant) ? $e['nombre'] + 1 : 1,
            'jusqua' => $maintenant + 5 * 60,
        ];
        ecrireJson(fichier('essais.json'), $essais);
        throw new ApiErreur('Identifiant ou mot de passe incorrect.', 401);
    }

    unset($essais[$ip]);
    ecrireJson(fichier('essais.json'), $essais);

    repondre(200, ['jeton' => ouvrirSession($staff[$idx]['id']), 'compte' => detailCompte($staff[$idx])]);
}

function deconnexion(): void
{
    $sessions = purgerSessions(lireJson(fichier('sessions.json'), []));
    unset($sessions[jetonBearer()]);
    ecrireJson(fichier('sessions.json'), $sessions);
    repondre(200, ['ok' => true]);
}

function modifierContenu(string $section, array $moi): void
{
    if (!isset(SECTIONS[$section])) {
        throw new ApiErreur('Section inconnue.', 404);
    }
    $corps = lireCorps();
    $valeur = $corps['valeur'] ?? null;
    $contenu = lireJson(fichier('contenu.json'), []);

    if (SECTIONS[$section]['objet'] ?? false) {
        $contenu[$section] = nettoyerElement($section, $valeur);
    } else {
        if (!estListe($valeur)) {
            throw new ApiErreur('Liste attendue.');
        }
        $contenu[$section] = array_map(
            fn($el) => nettoyerElement($section, $el),
            array_slice($valeur, 0, 200)
        );
    }
    ecrireJson(fichier('contenu.json'), $contenu);
    journal("{$moi['identifiant']} a modifié « $section »");
    repondre(200, (object) $contenu);
}

function modifierSoi(array $moi): void
{
    $corps = lireCorps();
    $staff = chargerStaff();
    $idx = indexParId($staff, $moi['id']);
    if ($idx === null) {
        throw new ApiErreur('Session expirée. Reconnecte-toi.', 401);
    }
    $c = $staff[$idx];
    if (!empty($corps['mdp'])) {
        if (!verifier((string) ($corps['ancienMdp'] ?? ''), $c)) {
            throw new ApiErreur('Le mot de passe actuel est incorrect.');
        }
        $mdp = (string) $corps['mdp'];
        validerMdp($mdp);
        $c['hash'] = hacher($mdp);
    }
    if (!empty($corps['nom'])) {
        $c['nom'] = texte($corps['nom'], 60);
    }
    if (array_key_exists('telephone', $corps)) {
        $c['telephone'] = texte($corps['telephone'], 20);
    }
    // Préférences qui suivent le joueur d'une tablette à l'autre
    if (isset($corps['fond'])) {
        if (!preg_match('/^[a-z]{1,20}$/D', (string) $corps['fond'])) {
            throw new ApiErreur('Fond d\'écran inconnu.');
        }
        $c['fond'] = (string) $corps['fond'];
    }
    if (array_key_exists('notes', $corps)) {
        if (!estListe($corps['notes'])) {
            throw new ApiErreur('Liste de notes attendue.');
        }
        if (count($corps['notes']) > 100) {
            throw new ApiErreur('100 notes maximum.');
        }
        $c['notes'] = array_map(fn($n) => [
            'id' => is_array($n) && is_numeric($n['id'] ?? null) ? (int) $n['id'] : 0,
            'texte' => texte(is_array($n) ? ($n['texte'] ?? '') : '', 10000),
        ], $corps['notes']);
    }
    $staff[$idx] = $c;
    sauverStaff($staff);
    repondre(200, detailCompte($c));
}

// Création d'un compte joueur depuis Réglages (connecte directement le joueur)
function inscription(): void
{
    $ip = $_SERVER['REMOTE_ADDR'] ?? 'inconnue';
    $maintenant = time();
    $essais = lireJson(fichier('essais.json'), []);
    $cle = 'inscription:' . $ip;
    $e = $essais[$cle] ?? null;
    if ($e && $e['nombre'] >= 5 && $e['jusqua'] > $maintenant) {
        throw new ApiErreur('Trop de comptes créés depuis cette connexion. Réessaie plus tard.', 429);
    }
    $corps = lireCorps();
    $identifiant = (string) ($corps['identifiant'] ?? '');
    $mdp = (string) ($corps['mdp'] ?? '');
    $nom = trim(texte($corps['nom'] ?? '', 60));
    validerIdentifiant($identifiant);
    validerMdp($mdp);
    if ($nom === '') {
        throw new ApiErreur('Indique le nom de ton personnage (Prénom Nom).');
    }
    garantirAdmin();
    $staff = chargerStaff();
    if (indexParIdentifiant($staff, $identifiant) !== null) {
        throw new ApiErreur('Cet identifiant est déjà pris.');
    }
    $compte = [
        'id' => uuid(), 'identifiant' => $identifiant, 'nom' => $nom, 'role' => 'joueur',
        'hash' => hacher($mdp), 'telephone' => texte($corps['telephone'] ?? '', 20), 'solde' => 0, 'historique' => [],
    ];
    $staff[] = $compte;
    sauverStaff($staff);
    $essais[$cle] = [
        'nombre' => ($e && $e['jusqua'] > $maintenant) ? $e['nombre'] + 1 : 1,
        'jusqua' => $maintenant + 3600,
    ];
    ecrireJson(fichier('essais.json'), $essais);
    journal("Nouveau compte joueur « $identifiant »");
    repondre(201, ['jeton' => ouvrirSession($compte['id']), 'compte' => detailCompte($compte)]);
}

function ouvrirSession(string $id): string
{
    $jeton = bin2hex(random_bytes(32));
    $sessions = purgerSessions(lireJson(fichier('sessions.json'), []));
    $sessions[$jeton] = ['id' => $id, 'expire' => time() + DUREE_SESSION];
    ecrireJson(fichier('sessions.json'), $sessions);
    return $jeton;
}

function creerCompte(array $moi): void
{
    $corps = lireCorps();
    $identifiant = (string) ($corps['identifiant'] ?? '');
    $mdp = (string) ($corps['mdp'] ?? '');
    $role = $corps['role'] ?? null;

    validerIdentifiant($identifiant);
    validerMdp($mdp);
    if (!in_array($role, ROLES, true)) {
        throw new ApiErreur('Rôle inconnu.');
    }
    $staff = chargerStaff();
    if (indexParIdentifiant($staff, $identifiant) !== null) {
        throw new ApiErreur('Cet identifiant existe déjà.');
    }
    $nom = (string) ($corps['nom'] ?? '');
    $compte = [
        'id' => uuid(),
        'identifiant' => $identifiant,
        'nom' => texte($nom !== '' ? $nom : $identifiant, 60),
        'role' => $role,
        'hash' => hacher($mdp),
    ];
    $staff[] = $compte;
    sauverStaff($staff);
    journal("{$moi['identifiant']} a créé le compte « $identifiant » ($role)");
    repondre(201, publicCompte($compte));
}

function modifierCompte(array $moi, array $staff, int $idx): void
{
    $corps = lireCorps();
    $c = $staff[$idx];
    if (!empty($corps['role'])) {
        if (!in_array($corps['role'], ROLES, true)) {
            throw new ApiErreur('Rôle inconnu.');
        }
        if ($corps['role'] !== 'admin' && $c['role'] === 'admin' && nombreAdmins($staff) === 1) {
            throw new ApiErreur('Il doit rester au moins un administrateur.');
        }
        $c['role'] = $corps['role'];
    }
    if (!empty($corps['mdp'])) {
        $mdp = (string) $corps['mdp'];
        validerMdp($mdp);
        $c['hash'] = hacher($mdp);
    }
    if (!empty($corps['nom'])) {
        $c['nom'] = texte($corps['nom'], 60);
    }
    $staff[$idx] = $c;
    sauverStaff($staff);
    repondre(200, publicCompte($c));
}

function supprimerCompte(array $moi, array $staff, int $idx): void
{
    $c = $staff[$idx];
    if ($c['role'] === 'admin' && nombreAdmins($staff) === 1) {
        throw new ApiErreur('Il doit rester au moins un administrateur.');
    }
    unset($staff[$idx]);
    sauverStaff($staff);
    $sessions = purgerSessions(lireJson(fichier('sessions.json'), []));
    $sessions = array_filter($sessions, fn($s) => $s['id'] !== $c['id']);
    ecrireJson(fichier('sessions.json'), $sessions);
    journal("{$moi['identifiant']} a supprimé le compte « {$c['identifiant']} »");
    repondre(200, ['ok' => true]);
}

// ---------- Gestion des entreprises ----------
// L'argent ici est celui de l'intranet : il n'est pas relié à la banque du jeu.
// Seul le staff peut créditer ou débiter la trésorerie d'une entreprise.

function chargerEntreprises(): array
{
    return lireJson(fichier('entreprises.json'), []);
}

function sauverEntreprises(array $liste): void
{
    ecrireJson(fichier('entreprises.json'), array_values($liste));
}

function indexEntreprise(array $liste, string $id): int
{
    foreach ($liste as $i => $e) {
        if ($e['id'] === $id) {
            return $i;
        }
    }
    throw new ApiErreur('Entreprise introuvable.', 404);
}

function membreDe(array $e, string $compteId): ?array
{
    foreach ($e['membres'] as $m) {
        if ($m['compteId'] === $compteId) {
            return $m;
        }
    }
    return null;
}

// Le staff agit comme un patron sur toutes les entreprises
function gradeDe(array $e, array $moi): ?string
{
    if (estStaff($moi)) {
        return 'patron';
    }
    $m = membreDe($e, $moi['id']);
    return $m['grade'] ?? null;
}

function exigerGrade(array $e, array $moi, array $grades): string
{
    $g = gradeDe($e, $moi);
    if ($g === null) {
        throw new ApiErreur("Tu ne fais pas partie de cette entreprise.", 403);
    }
    if (!in_array($g, $grades, true)) {
        throw new ApiErreur("Ton grade ne permet pas de faire ça.", 403);
    }
    return $g;
}

function montant($valeur): int
{
    if (!is_numeric($valeur) || (float) $valeur != floor((float) $valeur) || (int) $valeur <= 0 || (int) $valeur > 100000000) {
        throw new ApiErreur('Le montant doit être un nombre entier positif.');
    }
    return (int) $valeur;
}

function ajouterTransaction(array &$e, array $t): void
{
    array_unshift($e['transactions'], $t + ['id' => uuid(), 'date' => time()]);
    $e['transactions'] = array_slice($e['transactions'], 0, MAX_TRANSACTIONS);
}

function crediterJoueur(array &$staff, string $compteId, int $montant, string $libelle): void
{
    $idx = indexParId($staff, $compteId);
    if ($idx === null) {
        return;
    }
    $staff[$idx]['solde'] = (int) ($staff[$idx]['solde'] ?? 0) + $montant;
    $hist = $staff[$idx]['historique'] ?? [];
    array_unshift($hist, ['date' => time(), 'montant' => $montant, 'libelle' => $libelle]);
    $staff[$idx]['historique'] = array_slice($hist, 0, 50);
}

function resumeEntreprise(array $e, array $moi): array
{
    return [
        'id' => $e['id'], 'nom' => $e['nom'], 'secteur' => $e['secteur'],
        'monGrade' => estStaff($moi) && !membreDe($e, $moi['id']) ? 'staff' : (membreDe($e, $moi['id'])['grade'] ?? null),
        'effectif' => count($e['membres']),
    ];
}

function detailEntreprise(array $e, array $moi): array
{
    $staff = chargerStaff();
    $noms = [];
    foreach ($staff as $c) {
        $noms[$c['id']] = ['nom' => $c['nom'], 'identifiant' => $c['identifiant'], 'telephone' => $c['telephone'] ?? ''];
    }
    $grade = gradeDe($e, $moi);
    $gere = in_array($grade, ['patron', 'gerant'], true);
    $membres = array_map(fn($m) => $m + ($noms[$m['compteId']] ?? ['nom' => 'Compte supprimé', 'identifiant' => '', 'telephone' => '']), $e['membres']);
    // Un employé ne voit que les mouvements qui le concernent
    $transactions = $gere ? $e['transactions'] : array_values(array_filter($e['transactions'], fn($t) => ($t['vers'] ?? null) === $moi['id']));
    foreach ($transactions as &$t) {
        $t['versNom'] = isset($t['vers']) ? ($noms[$t['vers']]['nom'] ?? 'Compte supprimé') : null;
        $t['parNom'] = $noms[$t['par']]['nom'] ?? 'Compte supprimé';
    }
    return resumeEntreprise($e, $moi) + [
        'solde' => $gere ? (int) $e['solde'] : null,
        'regles' => $e['regles'],
        'membres' => $membres,
        'transactions' => $transactions,
        'peutGerer' => $gere,
        'estStaff' => estStaff($moi),
    ];
}

function routesEntreprises(string $methode, string $route, array $moi): void
{
    if ($route === 'entreprises' && $methode === 'GET') {
        $liste = array_filter(chargerEntreprises(), fn($e) => estStaff($moi) || membreDe($e, $moi['id']));
        repondre(200, array_values(array_map(fn($e) => resumeEntreprise($e, $moi), $liste)));
    }
    if ($route === 'entreprises' && $methode === 'POST') {
        if (!estStaff($moi)) {
            throw new ApiErreur('Seul le staff peut créer une entreprise.', 403);
        }
        $corps = lireCorps();
        $nom = trim(texte($corps['nom'] ?? '', 60));
        if ($nom === '') {
            throw new ApiErreur("Indique le nom de l'entreprise.");
        }
        $membres = [];
        $patron = trim((string) ($corps['patron'] ?? ''));
        if ($patron !== '') {
            $staff = chargerStaff();
            $idx = indexParIdentifiant($staff, $patron);
            if ($idx === null) {
                throw new ApiErreur("Aucun compte avec l'identifiant « $patron ».");
            }
            $membres[] = ['compteId' => $staff[$idx]['id'], 'grade' => 'patron', 'poste' => 'Patron', 'salaire' => 0];
        }
        $liste = chargerEntreprises();
        $e = ['id' => uuid(), 'nom' => $nom, 'secteur' => texte($corps['secteur'] ?? '', 60), 'solde' => 0,
            'creeLe' => time(), 'membres' => $membres, 'regles' => [], 'transactions' => []];
        $liste[] = $e;
        sauverEntreprises($liste);
        journal("{$moi['identifiant']} a créé l'entreprise « $nom »");
        repondre(201, detailEntreprise($e, $moi));
    }

    if (!preg_match('#^entreprises/([\w-]+)(?:/([a-z]+))?(?:/([\w-]+))?$#', $route, $m)) {
        return;
    }
    [$id, $sous, $cible] = [$m[1], $m[2] ?? '', $m[3] ?? ''];
    $liste = chargerEntreprises();
    $i = indexEntreprise($liste, $id);
    $e = $liste[$i];

    // Tchat : lecture et envoi pour tous les membres
    if ($sous === 'tchat') {
        exigerGrade($e, $moi, GRADES);
        $f = fichier("tchat-$id.json");
        if ($methode === 'GET') {
            $depuis = (int) ($_GET['depuis'] ?? 0);
            $messages = array_values(array_filter(lireJson($f, []), fn($msg) => $msg['t'] > $depuis));
            repondre(200, $messages);
        }
        if ($methode === 'POST') {
            $txt = trim(texte(lireCorps()['texte'] ?? '', 500));
            if ($txt === '') {
                throw new ApiErreur('Le message est vide.');
            }
            $messages = lireJson($f, []);
            $dernier = end($messages);
            $t = (int) round(microtime(true) * 1000);
            if ($dernier && $dernier['t'] >= $t) {
                $t = $dernier['t'] + 1;
            }
            $msg = ['id' => uuid(), 't' => $t, 'auteurId' => $moi['id'], 'nom' => $moi['nom'], 'texte' => $txt,
                'staff' => estStaff($moi) && !membreDe($e, $moi['id'])];
            $messages[] = $msg;
            ecrireJson($f, array_slice($messages, -MAX_MESSAGES));
            repondre(201, $msg);
        }
    }

    if ($sous === '' && $methode === 'GET') {
        exigerGrade($e, $moi, GRADES);
        repondre(200, detailEntreprise($e, $moi));
    }
    if ($sous === '' && $methode === 'PUT') {
        exigerGrade($e, $moi, ['patron']);
        $corps = lireCorps();
        if (isset($corps['nom']) && trim((string) $corps['nom']) !== '') {
            $e['nom'] = trim(texte($corps['nom'], 60));
        }
        if (isset($corps['secteur'])) {
            $e['secteur'] = texte($corps['secteur'], 60);
        }
        $liste[$i] = $e;
        sauverEntreprises($liste);
        repondre(200, detailEntreprise($e, $moi));
    }
    if ($sous === '' && $methode === 'DELETE') {
        if (!estStaff($moi)) {
            throw new ApiErreur('Seul le staff peut supprimer une entreprise.', 403);
        }
        unset($liste[$i]);
        sauverEntreprises($liste);
        @unlink(fichier("tchat-$id.json"));
        journal("{$moi['identifiant']} a supprimé l'entreprise « {$e['nom']} »");
        repondre(200, ['ok' => true]);
    }

    if ($sous === 'regles' && $methode === 'PUT') {
        exigerGrade($e, $moi, ['patron', 'gerant']);
        $regles = lireCorps()['regles'] ?? [];
        $regles = estListe($regles) ? $regles : [];
        $e['regles'] = array_values(array_filter(array_map(fn($r) => trim(texte($r, 500)), array_slice($regles, 0, 50)), fn($r) => $r !== ''));
        $liste[$i] = $e;
        sauverEntreprises($liste);
        repondre(200, detailEntreprise($e, $moi));
    }

    if ($sous === 'membres') {
        $monGrade = exigerGrade($e, $moi, GRADES);
        $corps = $methode === 'DELETE' ? [] : lireCorps();
        $poste = texte($corps['poste'] ?? '', 40);
        $salaire = isset($corps['salaire']) && $corps['salaire'] !== '' ? (int) max(0, min(100000000, (int) $corps['salaire'])) : null;
        $grade = $corps['grade'] ?? null;
        if ($grade !== null && !in_array($grade, GRADES, true)) {
            throw new ApiErreur('Grade inconnu.');
        }
        // Un gérant ne gère que les employés ; seul le patron (ou le staff) nomme gérants et patron
        $gererCible = function (?array $membre) use ($monGrade, $grade) {
            if ($monGrade === 'patron') {
                return;
            }
            if ($monGrade !== 'gerant' || ($membre && $membre['grade'] !== 'employe') || ($grade && $grade !== 'employe')) {
                throw new ApiErreur('Ton grade ne permet pas de faire ça.', 403);
            }
        };

        if ($methode === 'POST' && $cible === '') {
            $staff = chargerStaff();
            $idx = indexParIdentifiant($staff, (string) ($corps['identifiant'] ?? ''));
            if ($idx === null) {
                throw new ApiErreur("Aucun compte avec cet identifiant. Le joueur doit d'abord créer son compte dans Réglages.");
            }
            if (membreDe($e, $staff[$idx]['id'])) {
                throw new ApiErreur('Ce joueur fait déjà partie de l\'entreprise.');
            }
            if (count($e['membres']) >= 100) {
                throw new ApiErreur('100 employés maximum.');
            }
            $grade = $grade ?? 'employe';
            $gererCible(null);
            if ($grade === 'patron') {
                foreach ($e['membres'] as &$mb) {
                    if ($mb['grade'] === 'patron') {
                        $mb['grade'] = 'gerant';
                    }
                }
                unset($mb);
            }
            $e['membres'][] = ['compteId' => $staff[$idx]['id'], 'grade' => $grade, 'poste' => $poste !== '' ? $poste : ucfirst($grade === 'gerant' ? 'gérant' : $grade), 'salaire' => $salaire ?? 0];
        } else {
            $k = null;
            foreach ($e['membres'] as $n => $mb) {
                if ($mb['compteId'] === $cible) {
                    $k = $n;
                }
            }
            if ($k === null) {
                throw new ApiErreur('Employé introuvable.', 404);
            }
            if ($methode === 'DELETE') {
                // Chacun peut quitter l'entreprise, sauf le patron (il doit d'abord passer la main)
                if ($cible === $moi['id'] && $e['membres'][$k]['grade'] !== 'patron') {
                    // départ volontaire
                } else {
                    $gererCible($e['membres'][$k]);
                    if ($e['membres'][$k]['grade'] === 'patron' && !estStaff($moi)) {
                        throw new ApiErreur("Le patron ne peut pas être retiré. Nomme d'abord un autre patron.");
                    }
                }
                array_splice($e['membres'], $k, 1);
            } elseif ($methode === 'PUT') {
                $gererCible($e['membres'][$k]);
                if ($grade === 'patron') {
                    foreach ($e['membres'] as $n => $mb) {
                        if ($mb['grade'] === 'patron') {
                            $e['membres'][$n]['grade'] = 'gerant';
                        }
                    }
                }
                if ($grade !== null) {
                    $e['membres'][$k]['grade'] = $grade;
                }
                if ($poste !== '') {
                    $e['membres'][$k]['poste'] = $poste;
                }
                if ($salaire !== null) {
                    $e['membres'][$k]['salaire'] = $salaire;
                }
            } else {
                throw new ApiErreur('Route inconnue.', 404);
            }
        }
        $liste[$i] = $e;
        sauverEntreprises($liste);
        repondre(200, detailEntreprise($e, $moi));
    }

    if ($sous === 'virements' && $methode === 'POST') {
        exigerGrade($e, $moi, ['patron', 'gerant']);
        $corps = lireCorps();
        $somme = montant($corps['montant'] ?? null);
        $membre = membreDe($e, (string) ($corps['vers'] ?? ''));
        if (!$membre) {
            throw new ApiErreur('Choisis un employé de l\'entreprise.');
        }
        if ($somme > $e['solde']) {
            throw new ApiErreur("Solde insuffisant ({$e['solde']} € disponibles).");
        }
        $motif = trim(texte($corps['motif'] ?? '', 100)) ?: 'Virement';
        $e['solde'] -= $somme;
        ajouterTransaction($e, ['type' => 'virement', 'montant' => $somme, 'vers' => $membre['compteId'], 'motif' => $motif, 'par' => $moi['id']]);
        $staff = chargerStaff();
        crediterJoueur($staff, $membre['compteId'], $somme, "{$e['nom']} : $motif");
        sauverStaff($staff);
        $liste[$i] = $e;
        sauverEntreprises($liste);
        repondre(200, detailEntreprise($e, $moi));
    }

    if ($sous === 'salaires' && $methode === 'POST') {
        exigerGrade($e, $moi, ['patron', 'gerant']);
        $aPayer = array_filter($e['membres'], fn($mb) => $mb['salaire'] > 0);
        $total = array_sum(array_column($aPayer, 'salaire'));
        if ($total === 0) {
            throw new ApiErreur("Aucun salaire n'est défini. Indique un salaire dans la fiche de chaque employé.");
        }
        if ($total > $e['solde']) {
            throw new ApiErreur("Solde insuffisant : il faut $total € et l'entreprise a {$e['solde']} €.");
        }
        $staff = chargerStaff();
        foreach ($aPayer as $mb) {
            $e['solde'] -= $mb['salaire'];
            ajouterTransaction($e, ['type' => 'salaire', 'montant' => $mb['salaire'], 'vers' => $mb['compteId'], 'motif' => 'Salaire', 'par' => $moi['id']]);
            crediterJoueur($staff, $mb['compteId'], $mb['salaire'], "{$e['nom']} : salaire");
        }
        sauverStaff($staff);
        $liste[$i] = $e;
        sauverEntreprises($liste);
        repondre(200, detailEntreprise($e, $moi));
    }

    if ($sous === 'tresorerie' && $methode === 'POST') {
        if (!estStaff($moi)) {
            throw new ApiErreur('Seul le staff peut créditer ou débiter une entreprise.', 403);
        }
        $corps = lireCorps();
        $somme = montant($corps['montant'] ?? null);
        $type = $corps['type'] ?? '';
        if (!in_array($type, ['depot', 'retrait'], true)) {
            throw new ApiErreur('Type inconnu.');
        }
        if ($type === 'retrait' && $somme > $e['solde']) {
            throw new ApiErreur("Solde insuffisant ({$e['solde']} € disponibles).");
        }
        $e['solde'] += $type === 'depot' ? $somme : -$somme;
        ajouterTransaction($e, ['type' => $type, 'montant' => $somme, 'motif' => trim(texte($corps['motif'] ?? '', 100)) ?: ($type === 'depot' ? 'Dépôt' : 'Retrait'), 'par' => $moi['id']]);
        $liste[$i] = $e;
        sauverEntreprises($liste);
        journal("{$moi['identifiant']} : $type de $somme € sur « {$e['nom']} »");
        repondre(200, detailEntreprise($e, $moi));
    }

    throw new ApiErreur('Route inconnue.', 404);
}

function dispatch(): void
{
    if (!is_dir(dossierData())) {
        mkdir(dossierData(), 0700, true);
    }
    $methode = $_SERVER['REQUEST_METHOD'] ?? 'GET';
    $route = trim((string) ($_GET['route'] ?? ''), '/');
    if ($methode !== 'GET') {
        verrouiller();
    }

    // Routes publiques
    if ($methode === 'GET' && $route === 'contenu') {
        repondre(200, (object) lireJson(fichier('contenu.json'), []));
    }
    if ($methode === 'POST' && $route === 'connexion') {
        connexion();
    }
    if ($methode === 'POST' && $route === 'inscription') {
        inscription();
    }

    // Routes qui demandent une session (joueurs, staff et admins)
    $moi = compteDepuisRequete();
    if ($moi === null) {
        throw new ApiErreur('Session expirée. Reconnecte-toi.', 401);
    }
    if ($methode === 'POST' && $route === 'deconnexion') {
        deconnexion();
    }
    if ($methode === 'GET' && $route === 'moi') {
        repondre(200, detailCompte($moi));
    }
    if ($methode === 'PUT' && $route === 'moi') {
        modifierSoi($moi);
    }
    if (strncmp($route, 'entreprises', 11) === 0) {
        routesEntreprises($methode, $route, $moi);
    }

    // Contenu de la tablette et liste des comptes : staff et admins
    if (!estStaff($moi)) {
        throw new ApiErreur('Réservé au staff.', 403);
    }
    if ($methode === 'PUT' && preg_match('#^contenu/([a-z]+)$#', $route, $m)) {
        modifierContenu($m[1], $moi);
    }
    if ($methode === 'GET' && $route === 'staff') {
        repondre(200, array_map('publicCompte', chargerStaff()));
    }
    if ($methode === 'PUT' && preg_match('#^staff/([\w-]+)$#', $route, $m) && $m[1] === $moi['id']) {
        modifierSoi($moi);
    }

    // Le reste est réservé aux administrateurs
    if (($moi['role'] ?? '') !== 'admin') {
        throw new ApiErreur('Réservé aux administrateurs.', 403);
    }
    if ($methode === 'POST' && $route === 'staff') {
        creerCompte($moi);
    }
    if (preg_match('#^staff/([\w-]+)$#', $route, $m)) {
        $staff = chargerStaff();
        $idx = indexParId($staff, $m[1]);
        if ($idx === null) {
            throw new ApiErreur('Compte introuvable.', 404);
        }
        if ($methode === 'PUT') {
            modifierCompte($moi, $staff, $idx);
        }
        if ($methode === 'DELETE') {
            supprimerCompte($moi, $staff, $idx);
        }
    }

    throw new ApiErreur('Route inconnue.', 404);
}

try {
    dispatch();
} catch (ApiErreur $e) {
    repondre($e->statut, ['erreur' => $e->getMessage()]);
} catch (Throwable $e) {
    journal('Erreur : ' . $e->getMessage());
    repondre(500, ['erreur' => 'Erreur interne du serveur.']);
}
