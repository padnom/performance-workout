# Performance — app Android .NET

Migration **.NET MAUI Blazor Hybrid** destinée à Android, structurée en Clean Architecture :

- `Performance.Domain` : modèles métier sans dépendance technique ;
- `Performance.Application` : ports et cas d’usage ;
- `Performance.Infrastructure` : persistance JSON atomique et migration React ;
- `Performance.App` : hôte MAUI et interface Blazor ;
- `Performance.Application.Tests` et `Performance.Infrastructure.Tests` : tests xUnit.

L’interface est embarquée dans l’application : l’objectif est un APK autonome, utilisable hors ligne et sans dépendre de l’hébergement du site actuel.

## Prérequis

- .NET 10 SDK
- workload MAUI Android (`dotnet workload install maui-android`)
- Android SDK / Android Studio, avec une plateforme Android API 23 ou ultérieure

## Compilation Android

Depuis ce dossier :

```sh
dotnet workload restore Performance.sln
dotnet build src/Performance.App/Performance.App.csproj -f net10.0-android
dotnet test Performance.sln
```

Les pages **Programmes**, **Exercices**, **Progrès**, **Historique** et **Réglages** sont les premiers écrans natifs. L’import accepte l’ancien tableau d’historique ainsi que la sauvegarde React complète `schemaVersion: 2`. Il conserve les identifiants d’exercices et les historiques séparés par salle.

La version React doit être utilisée pour générer une sauvegarde complète avant toute désinstallation. L’APK n’accède pas directement au `localStorage` de la PWA : le transfert passe volontairement par le fichier JSON exporté.
