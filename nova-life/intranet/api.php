<?php
/*
 * Intranet Nova : API du serveur, version PHP (hébergement Web Infomaniak).
 * Même API que l'ancien server.js. Appelée via .htaccess : /api/<route> -> api.php?route=<route>.
 * Configuration : fichier .env à côté de ce fichier (ADMIN_MDP, ADMIN_IDENTIFIANT).
 * Données : dossier data/ (protégé par .htaccess).
 */

ini_set('display_errors', '0');
error_reporting(E_ALL);

const DUREE_SESSION = 12 * 3600;
const ROLES = ['admin', 'staff'];

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

    $jeton = bin2hex(random_bytes(32));
    $sessions = purgerSessions(lireJson(fichier('sessions.json'), []));
    $sessions[$jeton] = ['id' => $staff[$idx]['id'], 'expire' => $maintenant + DUREE_SESSION];
    ecrireJson(fichier('sessions.json'), $sessions);

    repondre(200, ['jeton' => $jeton, 'compte' => publicCompte($staff[$idx])]);
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
        if ($corps['role'] === 'staff' && $c['role'] === 'admin' && nombreAdmins($staff) === 1) {
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

function dispatch(): void
{
    if (!is_dir(dossierData())) {
        mkdir(dossierData(), 0700, true);
    }
    $methode = $_SERVER['REQUEST_METHOD'] ?? 'GET';
    $route = trim((string) ($_GET['route'] ?? ''), '/');

    // Routes publiques
    if ($methode === 'GET' && $route === 'contenu') {
        repondre(200, (object) lireJson(fichier('contenu.json'), []));
    }
    if ($methode === 'POST' && $route === 'connexion') {
        connexion();
    }

    // Routes qui demandent une session
    $moi = compteDepuisRequete();
    if ($moi === null) {
        throw new ApiErreur('Session expirée. Reconnecte-toi.', 401);
    }
    if ($methode === 'POST' && $route === 'deconnexion') {
        deconnexion();
    }
    if ($methode === 'GET' && $route === 'moi') {
        repondre(200, publicCompte($moi));
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
