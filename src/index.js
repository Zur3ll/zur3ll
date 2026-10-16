require('dotenv').config();

const {
  Client,
  GatewayIntentBits,
  Events,
  EmbedBuilder,
  ActionRowBuilder,
  ButtonBuilder,
  ButtonStyle,
  ModalBuilder,
  TextInputBuilder,
  TextInputStyle,
} = require('discord.js');

const { DISCORD_TOKEN, SALON_RDV_ID, SALON_COMMANDE_ID, SALON_RECEPTION_ID } = process.env;

for (const [nom, valeur] of Object.entries({ DISCORD_TOKEN, SALON_RDV_ID, SALON_COMMANDE_ID, SALON_RECEPTION_ID })) {
  if (!valeur) {
    console.error(`❌ Variable manquante dans le fichier .env : ${nom}`);
    process.exit(1);
  }
}

// ---------------------------------------------------------------------------
// Configuration des deux formulaires
// Discord limite un pop-up à 5 champs maximum et un libellé à 45 caractères.
// ---------------------------------------------------------------------------
const FORMULAIRES = {
  rdv: {
    salonId: SALON_RDV_ID,
    boutonId: 'ouvrir_rdv',
    modalId: 'modal_rdv',
    boutonLabel: 'Prendre rendez-vous',
    boutonEmoji: '🔧',
    couleur: 0xf1c40f,
    panneau: {
      titre: '🔧 Réserver une intervention au garage',
      description:
        'Pour réserver une intervention au garage, clique sur le bouton ci-dessous et remplis le formulaire.\n' +
        'Un mécanicien te répondra au plus vite ⏱️\n\n' +
        '⚠️ *Merci de prévenir en cas d\'annulation.*',
    },
    modalTitre: 'Prise de rendez-vous – Garage',
    receptionTitre: '🔧 Nouvelle demande de rendez-vous',
    champs: [
      { id: 'nom_rp', label: 'Nom RP', style: TextInputStyle.Short, placeholder: 'Ex : John Doe' },
      { id: 'vehicule', label: 'Véhicule', style: TextInputStyle.Short, placeholder: 'Ex : Sultan RS' },
      { id: 'probleme', label: 'Problème / service souhaité', style: TextInputStyle.Paragraph, placeholder: 'Décris le problème ou le service voulu' },
      { id: 'dispos', label: 'Disponibilités', style: TextInputStyle.Paragraph, placeholder: 'Ex : ce soir à partir de 21h' },
    ],
  },

  commande: {
    salonId: SALON_COMMANDE_ID,
    boutonId: 'ouvrir_commande',
    modalId: 'modal_commande',
    boutonLabel: 'Commander un véhicule',
    boutonEmoji: '🛒',
    couleur: 0x3498db,
    panneau: {
      titre: '🛒 Commander un véhicule',
      description:
        'Remplis ce formulaire pour passer commande :\n' +
        '💳 Le paiement se fait **à la remise du véhicule** en jeu.\n' +
        'Un vendeur prendra contact avec toi rapidement. 🚘',
    },
    modalTitre: 'Commande de véhicule',
    receptionTitre: '🛒 Nouvelle commande de véhicule',
    champs: [
      { id: 'nom_rp', label: 'Nom RP', style: TextInputStyle.Short, placeholder: 'Ex : John Doe' },
      { id: 'vehicule', label: 'Véhicule souhaité', style: TextInputStyle.Short, placeholder: 'Ex : Elegy RH8' },
      // Couleur + Options regroupés : un pop-up Discord accepte 5 champs maximum.
      { id: 'couleur_options', label: 'Couleur / Options (jantes, tuning…)', style: TextInputStyle.Paragraph, placeholder: 'Ex : Noir mat, jantes chromées, full tuning' },
      { id: 'budget', label: 'Budget', style: TextInputStyle.Short, placeholder: 'Ex : 150 000 €' },
      { id: 'dispos', label: 'Disponibilités pour la livraison', style: TextInputStyle.Paragraph, placeholder: 'Ex : demain après 20h' },
    ],
  },
};

const client = new Client({ intents: [GatewayIntentBits.Guilds] });

// Bouton posté sous chaque demande reçue, pour dire qui s'en occupe.
const BOUTON_PRISE_ID = 'prendre_ticket';

function creerBoutonPrise(libelle = 'Je me charge du ticket', desactive = false) {
  const bouton = new ButtonBuilder()
    .setCustomId(BOUTON_PRISE_ID)
    .setLabel(libelle)
    .setEmoji('🎫')
    .setStyle(desactive ? ButtonStyle.Secondary : ButtonStyle.Success)
    .setDisabled(desactive);
  return new ActionRowBuilder().addComponents(bouton);
}

// Un seul membre peut prendre le ticket : le bouton devient gris et affiche son nom.
async function prendreTicket(interaction) {
  const nom = interaction.member?.displayName ?? interaction.user.username;
  const embed = EmbedBuilder.from(interaction.message.embeds[0]).addFields({
    name: 'Pris en charge par',
    value: `${interaction.user}`,
  });

  await interaction.update({
    embeds: [embed],
    components: [creerBoutonPrise(`Pris en charge par ${nom}`, true)],
  });
  // Un message classique (pas un champ d'embed) est nécessaire pour que la personne soit notifiée.
  await interaction.followUp({ content: `${interaction.user} se charge de cette demande 🎫` });
}

function creerPanneau(form) {
  const embed = new EmbedBuilder()
    .setTitle(form.panneau.titre)
    .setDescription(form.panneau.description)
    .setColor(form.couleur);

  const bouton = new ButtonBuilder()
    .setCustomId(form.boutonId)
    .setLabel(form.boutonLabel)
    .setEmoji(form.boutonEmoji)
    .setStyle(ButtonStyle.Primary);

  return { embeds: [embed], components: [new ActionRowBuilder().addComponents(bouton)] };
}

function creerModal(form) {
  const modal = new ModalBuilder().setCustomId(form.modalId).setTitle(form.modalTitre);

  for (const champ of form.champs) {
    const input = new TextInputBuilder()
      .setCustomId(champ.id)
      .setLabel(champ.label)
      .setStyle(champ.style)
      .setPlaceholder(champ.placeholder)
      .setRequired(true)
      .setMaxLength(champ.style === TextInputStyle.Short ? 100 : 1000);
    modal.addComponents(new ActionRowBuilder().addComponents(input));
  }

  return modal;
}

// Publie le panneau dans le salon, sauf s'il y est déjà (évite les doublons à chaque redémarrage).
async function publierPanneau(form) {
  const salon = await client.channels.fetch(form.salonId).catch(() => null);
  if (!salon || !salon.isTextBased()) {
    console.error(`❌ Salon introuvable ou non textuel : ${form.salonId}`);
    return;
  }

  const messages = await salon.messages.fetch({ limit: 50 });
  const dejaPublie = messages.some(
    (m) =>
      m.author.id === client.user.id &&
      m.components.some((ligne) => ligne.components.some((c) => c.customId === form.boutonId)),
  );

  if (dejaPublie) {
    console.log(`ℹ️  Panneau déjà présent dans #${salon.name}`);
    return;
  }

  await salon.send(creerPanneau(form));
  console.log(`✅ Panneau publié dans #${salon.name}`);
}

client.once(Events.ClientReady, async () => {
  console.log(`🤖 Connecté en tant que ${client.user.tag}`);
  for (const form of Object.values(FORMULAIRES)) {
    await publierPanneau(form).catch((err) => console.error('Erreur publication panneau :', err));
  }
});

client.on(Events.InteractionCreate, async (interaction) => {
  try {
    // Clic sur un bouton -> ouverture du pop-up
    if (interaction.isButton()) {
      if (interaction.customId === BOUTON_PRISE_ID) return await prendreTicket(interaction);

      const form = Object.values(FORMULAIRES).find((f) => f.boutonId === interaction.customId);
      if (form) await interaction.showModal(creerModal(form));
      return;
    }

    // Validation du pop-up -> envoi dans le salon de réception
    if (interaction.isModalSubmit()) {
      const form = Object.values(FORMULAIRES).find((f) => f.modalId === interaction.customId);
      if (!form) return;

      const reception = await client.channels.fetch(SALON_RECEPTION_ID).catch(() => null);
      if (!reception || !reception.isTextBased()) {
        await interaction.reply({ content: '❌ Le salon de réception est introuvable, préviens un administrateur.', ephemeral: true });
        return;
      }

      const embed = new EmbedBuilder()
        .setTitle(form.receptionTitre)
        .setColor(form.couleur)
        .setAuthor({ name: interaction.user.tag, iconURL: interaction.user.displayAvatarURL() })
        .addFields(
          { name: 'Demandé par', value: `${interaction.user}` },
          ...form.champs.map((champ) => ({
            name: champ.label,
            value: interaction.fields.getTextInputValue(champ.id),
          })),
        )
        .setTimestamp();

      await reception.send({ embeds: [embed], components: [creerBoutonPrise()] });
      await interaction.reply({
        content: '✅ Ta demande a bien été envoyée ! Quelqu\'un te répondra au plus vite.',
        ephemeral: true,
      });
    }
  } catch (err) {
    console.error('Erreur interaction :', err);
    if (interaction.isRepliable() && !interaction.replied && !interaction.deferred) {
      await interaction.reply({ content: '❌ Une erreur est survenue, réessaie plus tard.', ephemeral: true }).catch(() => {});
    }
  }
});

client.login(DISCORD_TOKEN);
