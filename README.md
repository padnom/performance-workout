# Performance — application Android

Application de suivi de musculation en **C# / .NET 10 MAUI Blazor Hybrid**, utilisable hors connexion et sans hébergement. L’interface, le catalogue et les données sont embarqués dans l’APK.

Version actuelle : **1.4.0**, code Android **7**.

## Fonctionnalités

- Accueil et programme de la semaine : ajout, réorganisation et retrait des séances.
- Bibliothèque de programmes, variantes et démarrage rapide d’une séance libre.
- Création de programmes personnels à plusieurs séances, réglage des exercices et favoris pour retrouver rapidement ses programmes.
- Création d’une séance seule et partage d’une séance (historique ou programme) en fichier ou en texte, par exemple sur WhatsApp ; un fichier reçu s’ouvre directement dans l’application et s’ajoute à « Mes séances ».
- Programme de quatre séances adapté à One Air ou Basic-Fit : Push, Pull, Jambes et Upper, en 4×10 modifiable. Deux exercices d’abdos peuvent être ajoutés volontairement en fin de séance.
- Partage JSON de la semaine ou de programmes sélectionnés, et import de copies après aperçu, sans partager les charges ni l’historique personnel.
- Charges et répétitions par salle, remplacement/modification des exercices et exercices personnalisés.
- Sauvegarde et reprise de la séance en cours après interruption.
- Barre compacte en haut : durée de la séance, séries renseignées, repos et terminaison.
- Vibration discrète de fin de repos, optionnelle et désactivée par défaut.
- Historique, records par salle, graphiques de progression et répartition des séries par muscle.
- Import/export des sauvegardes Android et de l’ancien site web.
- Diagnostics locaux exportés uniquement à la demande.
- Mise à jour depuis l’application : détection automatique des nouvelles versions publiées sur GitHub, téléchargement vérifié puis installation confirmée par Android.
- Couleurs adaptées à la salle : vert One Air, orange Basic-Fit, selon la séance en cours ou la salle choisie.

Le RIR, le poids cumulé soulevé et le temps cumulé en salle ne sont pas affichés. Les anciennes données restent compatibles avec les imports.

## Organisation du dépôt

```text
dotnet/               application Android, tests et scripts de livraison
legacy/web/           ancien site React conservé comme référence
specs/android-migration/ décisions et preuves de migration
.github/workflows/    vérification et fabrication de l’APK Android
outputs/              APK et captures locaux, exclus de Git
```

Les couches Domain, Application et Infrastructure sont indépendantes de l’hôte Android. Les pages Blazor et les services natifs sont dans `Performance.App`.

## Développer et vérifier

Prérequis : SDK .NET 10, workload MAUI Android, JDK 21 et Android SDK avec plateforme/build-tools 36. Android API 23 minimum.

Depuis la racine :

```sh
dotnet workload restore dotnet/Performance.slnx
dotnet build dotnet/src/Performance.App/Performance.App.csproj -f net10.0-android
dotnet test dotnet/tests/Performance.Application.UnitTests/Performance.Application.UnitTests.csproj -c Release
dotnet test dotnet/tests/Performance.Infrastructure.UnitTests/Performance.Infrastructure.UnitTests.csproj -c Release
dotnet test dotnet/tests/Performance.Infrastructure.IntegrationTests/Performance.Infrastructure.IntegrationTests.csproj -c Release
dotnet test dotnet/tests/Performance.App.UnitTests/Performance.App.UnitTests.csproj -c Release
python3 -m unittest discover -s dotnet/scripts/tests -v
```

Voir [le projet Android](dotnet/README.md) et [la compilation, la signature et les essais](dotnet/README-ANDROID.md).

## Installer et livrer

L’APK de cette version se trouve dans `outputs/performance-android-1.4.0.apk` après compilation et vérification. Installer une mise à jour par-dessus l’application existante pour conserver ses données ; une désinstallation les efface.

La CI vérifie les tests, les versions, un APK Release avec AOT/trimming et son lancement sur émulateur. Son APK de diagnostic utilise une clé jetable et ne met pas à jour l’installation existante. La distribution compatible nécessite les quatre secrets du certificat actuel dans l’environnement GitHub `android-release`, comme décrit dans le guide Android. Aucune clé privée ni APK n’est versionné.

L’ancien site est [archivé sous legacy/web](legacy/web/README.md). Il sert de référence pour les comportements et le format des sauvegardes ; son pipeline n’est plus actif. Le déplacement des sources ne modifie pas le site déjà déployé.
