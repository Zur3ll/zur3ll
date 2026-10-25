// Serveur de l'intranet Nova : sert la page de la tablette, enregistre le contenu modifié
// par le staff et gère les comptes staff. Aucune dépendance : Node.js 18 ou plus suffit.
//   node server.js            (port 3000 par défaut, ou la variable d'environnement PORT)

const http = require("http");
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");

const PORT = Number(process.env.PORT) || 3000;
const DOSSIER_DONNEES = path.join(__dirname, "data");
const FICHIER_CONTENU = path.join(DOSSIER_DONNEES, "contenu.json");
const FICHIER_STAFF = path.join(DOSSIER_DONNEES, "staff.json");
const PAGE = path.join(__dirname, "index.html");
const DUREE_SESSION_MS = 12 * 60 * 60 * 1000;

// Sections modifiables et champs autorisés pour chaque élément (le reste est ignoré)
const SECTIONS = {
  general: { objet: true, champs: ["serveur", "ville", "discord"] },
  actualites: { champs: ["titre", "date", "auteur", "texte"] },
  evenements: { champs: ["titre", "date", "heure", "lieu"] },
  entreprises: { champs: ["nom", "secteur", "statut", "patron", "tel", "description"] },
  emplois: { champs: ["poste", "entreprise", "salaire", "details"] },
  annuaire: { champs: ["nom", "role", "entreprise", "tel"] },
  reglement: { champs: ["titre", "regles"] },
  liens: { champs: ["nom", "url", "description"] },
};
const ROLES = ["admin", "staff"];

// ---------- Stockage ----------

fs.mkdirSync(DOSSIER_DONNEES, { recursive: true });

function lire(fichier, defaut) {
  try { return JSON.parse(fs.readFileSync(fichier, "utf8")); } catch { return defaut; }
}
function ecrire(fichier, valeur) {
  const tmp = fichier + ".tmp";
  fs.writeFileSync(tmp, JSON.stringify(valeur, null, 2));
  fs.renameSync(tmp, fichier);
}

let contenu = lire(FICHIER_CONTENU, {});
let staff = lire(FICHIER_STAFF, []);

// ---------- Mots de passe et sessions ----------

function hacher(mdp, sel = crypto.randomBytes(16).toString("hex")) {
  return { sel, hash: crypto.scryptSync(mdp, sel, 64).toString("hex") };
}
function verifier(mdp, compte) {
  const essai = Buffer.from(hacher(mdp, compte.sel).hash, "hex");
  const attendu = Buffer.from(compte.hash, "hex");
  return essai.length === attendu.length && crypto.timingSafeEqual(essai, attendu);
}

// Premier démarrage : un compte admin est créé et son mot de passe affiché une seule fois
if (staff.length === 0) {
  const mdp = crypto.randomBytes(9).toString("base64url");
  staff.push({ id: crypto.randomUUID(), identifiant: "admin", nom: "Administrateur", role: "admin", ...hacher(mdp) });
  ecrire(FICHIER_STAFF, staff);
  console.log("==============================================================");
  console.log(" Premier démarrage : compte staff créé");
  console.log(`   Identifiant : admin`);
  console.log(`   Mot de passe : ${mdp}`);
  console.log(" Change-le depuis l'app Staff de la tablette après connexion.");
  console.log("==============================================================");
}

const sessions = new Map(); // jeton -> { id, expire }
const essais = new Map();   // ip -> { nombre, jusqua }

function compteDepuis(req) {
  const jeton = (req.headers.authorization || "").replace(/^Bearer /, "");
  const s = sessions.get(jeton);
  if (!s || s.expire < Date.now()) { sessions.delete(jeton); return null; }
  return staff.find((c) => c.id === s.id) || null;
}
const publicCompte = (c) => ({ id: c.id, identifiant: c.identifiant, nom: c.nom, role: c.role });

// ---------- Nettoyage des données reçues ----------

const texte = (v, max = 2000) => String(v ?? "").slice(0, max);

function nettoyerElement(section, el) {
  const propre = {};
  for (const champ of SECTIONS[section].champs) {
    if (champ === "regles") propre.regles = (Array.isArray(el?.regles) ? el.regles : []).slice(0, 50).map((r) => texte(r, 500)).filter(Boolean);
    else propre[champ] = texte(el?.[champ]);
  }
  if (section === "liens" && !/^https?:\/\//i.test(propre.url)) throw new Error("Chaque lien doit commencer par http:// ou https://.");
  if (section === "general" && propre.discord && !/^https?:\/\//i.test(propre.discord)) throw new Error("Le lien Discord doit commencer par http:// ou https://.");
  return propre;
}

function validerIdentifiant(id) {
  if (!/^[a-zA-Z0-9._-]{3,32}$/.test(id || "")) throw new Error("L'identifiant doit faire 3 à 32 caractères (lettres, chiffres, point, tiret).");
}
function validerMdp(mdp) {
  if (typeof mdp !== "string" || mdp.length < 8) throw new Error("Le mot de passe doit faire au moins 8 caractères.");
}

// ---------- HTTP ----------

function repondre(res, code, donnees) {
  res.writeHead(code, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store" });
  res.end(JSON.stringify(donnees));
}
function corps(req) {
  return new Promise((ok, ko) => {
    let taille = 0; const morceaux = [];
    req.on("data", (m) => { taille += m.length; if (taille > 1_000_000) { ko(new Error("Données trop volumineuses.")); req.destroy(); } else morceaux.push(m); });
    req.on("end", () => { try { ok(morceaux.length ? JSON.parse(Buffer.concat(morceaux).toString("utf8")) : {}); } catch { ko(new Error("Données illisibles.")); } });
    req.on("error", ko);
  });
}

async function api(req, res, url) {
  const route = `${req.method} ${url.pathname}`;

  if (route === "GET /api/contenu") return repondre(res, 200, contenu);

  if (route === "POST /api/connexion") {
    const ip = req.socket.remoteAddress;
    const e = essais.get(ip);
    if (e && e.nombre >= 5 && e.jusqua > Date.now()) return repondre(res, 429, { erreur: "Trop d'essais. Réessaie dans quelques minutes." });
    const { identifiant, mdp } = await corps(req);
    const compte = staff.find((c) => c.identifiant.toLowerCase() === String(identifiant || "").toLowerCase());
    if (!compte || !verifier(String(mdp || ""), compte)) {
      const n = e && e.jusqua > Date.now() ? e.nombre + 1 : 1;
      essais.set(ip, { nombre: n, jusqua: Date.now() + 5 * 60 * 1000 });
      return repondre(res, 401, { erreur: "Identifiant ou mot de passe incorrect." });
    }
    essais.delete(ip);
    const jeton = crypto.randomBytes(32).toString("hex");
    sessions.set(jeton, { id: compte.id, expire: Date.now() + DUREE_SESSION_MS });
    return repondre(res, 200, { jeton, compte: publicCompte(compte) });
  }

  const moi = compteDepuis(req);
  if (!moi) return repondre(res, 401, { erreur: "Session expirée. Reconnecte-toi." });

  if (route === "POST /api/deconnexion") {
    sessions.delete((req.headers.authorization || "").replace(/^Bearer /, ""));
    return repondre(res, 200, { ok: true });
  }
  if (route === "GET /api/moi") return repondre(res, 200, publicCompte(moi));

  const section = url.pathname.match(/^\/api\/contenu\/([a-z]+)$/)?.[1];
  if (req.method === "PUT" && section) {
    if (!SECTIONS[section]) return repondre(res, 404, { erreur: "Section inconnue." });
    const { valeur } = await corps(req);
    if (SECTIONS[section].objet) contenu[section] = nettoyerElement(section, valeur);
    else {
      if (!Array.isArray(valeur)) throw new Error("Liste attendue.");
      contenu[section] = valeur.slice(0, 200).map((el) => nettoyerElement(section, el));
    }
    ecrire(FICHIER_CONTENU, contenu);
    console.log(`[intranet] ${moi.identifiant} a modifié « ${section} »`);
    return repondre(res, 200, contenu);
  }

  // Comptes staff : la liste et le changement de son propre mot de passe sont ouverts à tous les comptes,
  // le reste est réservé aux admins
  if (route === "GET /api/staff") return repondre(res, 200, staff.map(publicCompte));

  const idCible = url.pathname.match(/^\/api\/staff\/([\w-]+)$/)?.[1];
  if (req.method === "PUT" && idCible === moi.id) {
    const { mdp, nom } = await corps(req);
    if (mdp) { validerMdp(mdp); Object.assign(moi, hacher(mdp)); }
    if (nom) moi.nom = texte(nom, 60);
    ecrire(FICHIER_STAFF, staff);
    return repondre(res, 200, publicCompte(moi));
  }

  if (moi.role !== "admin") return repondre(res, 403, { erreur: "Réservé aux administrateurs." });

  if (route === "POST /api/staff") {
    const { identifiant, mdp, nom, role } = await corps(req);
    validerIdentifiant(identifiant); validerMdp(mdp);
    if (!ROLES.includes(role)) throw new Error("Rôle inconnu.");
    if (staff.some((c) => c.identifiant.toLowerCase() === identifiant.toLowerCase())) throw new Error("Cet identifiant existe déjà.");
    const compte = { id: crypto.randomUUID(), identifiant, nom: texte(nom || identifiant, 60), role, ...hacher(mdp) };
    staff.push(compte); ecrire(FICHIER_STAFF, staff);
    console.log(`[intranet] ${moi.identifiant} a créé le compte « ${identifiant} » (${role})`);
    return repondre(res, 201, publicCompte(compte));
  }

  const cible = idCible && staff.find((c) => c.id === idCible);
  if (idCible && !cible) return repondre(res, 404, { erreur: "Compte introuvable." });
  const admins = () => staff.filter((c) => c.role === "admin").length;

  if (req.method === "PUT" && cible) {
    const { mdp, nom, role } = await corps(req);
    if (role && !ROLES.includes(role)) throw new Error("Rôle inconnu.");
    if (role === "staff" && cible.role === "admin" && admins() === 1) throw new Error("Il doit rester au moins un administrateur.");
    if (mdp) { validerMdp(mdp); Object.assign(cible, hacher(mdp)); }
    if (nom) cible.nom = texte(nom, 60);
    if (role) cible.role = role;
    ecrire(FICHIER_STAFF, staff);
    return repondre(res, 200, publicCompte(cible));
  }
  if (req.method === "DELETE" && cible) {
    if (cible.role === "admin" && admins() === 1) throw new Error("Il doit rester au moins un administrateur.");
    staff = staff.filter((c) => c !== cible); ecrire(FICHIER_STAFF, staff);
    for (const [j, s] of sessions) if (s.id === cible.id) sessions.delete(j);
    console.log(`[intranet] ${moi.identifiant} a supprimé le compte « ${cible.identifiant} »`);
    return repondre(res, 200, { ok: true });
  }

  return repondre(res, 404, { erreur: "Route inconnue." });
}

http.createServer(async (req, res) => {
  const url = new URL(req.url, "http://localhost");
  try {
    if (url.pathname.startsWith("/api/")) return await api(req, res, url);
    if (req.method === "GET" && (url.pathname === "/" || url.pathname === "/index.html")) {
      res.writeHead(200, { "Content-Type": "text/html; charset=utf-8", "Cache-Control": "no-cache" });
      return fs.createReadStream(PAGE).pipe(res);
    }
    res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" }); res.end("Page introuvable");
  } catch (e) {
    repondre(res, 400, { erreur: e.message || "Requête invalide." });
  }
}).listen(PORT, () => console.log(`[intranet] En ligne sur le port ${PORT}`));
