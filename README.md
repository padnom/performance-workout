# Performance — application Android

Application de suivi de musculation en **C# / .NET 10 MAUI Blazor Hybrid**, utilisable hors connexion et sans hébergement. L’interface, le catalogue et les données sont embarqués dans l’APK.

Version actuelle : **1.1.1**, code Android **3**.

## Fonctionnalités

- Accueil et programme de la semaine : ajout, réorganisation et retrait des séances.
- Bibliothèque de programmes, variantes et démarrage rapide d’une séance libre.
- Charges et répétitions par salle, remplacement/modification des exercices et exercices personnalisés.
- Sauvegarde et reprise de la séance en cours après interruption.
- Barre compacte en haut : durée de la séance, séries renseignées, repos et terminaison.
- Vibration discrète de fin de repos, optionnelle et désactivée par défaut.
- Historique, records par salle, graphiques de progression et répartition des séries par muscle.
- Import/export des sauvegardes Android et de l’ancien site web.
- Diagnostics locaux exportés uniquement à la demande.

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

L’APK livré localement se trouve dans `outputs/performance-android-1.1.1.apk`. Installer une mise à jour par-dessus l’application existante pour conserver ses données ; une désinstallation les efface.

La CI vérifie les tests, les versions, un APK Release avec AOT/trimming et son lancement sur émulateur. Son APK de diagnostic utilise une clé jetable et ne met pas à jour l’installation existante. La distribution compatible nécessite les quatre secrets du certificat actuel dans l’environnement GitHub `android-release`, comme décrit dans le guide Android. Aucune clé privée ni APK n’est versionné.

L’ancien site est [archivé sous legacy/web](legacy/web/README.md). Il sert de référence pour les comportements et le format des sauvegardes ; son pipeline n’est plus actif. Le déplacement des sources ne modifie pas le site déjà déployé.
