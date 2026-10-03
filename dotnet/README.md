# Performance — app Android .NET

Application **.NET MAUI Blazor Hybrid** destinée à Android, organisée par couches :

- `Performance.Domain` : modèles métier sans dépendance technique ;
- `Performance.Application` : ports et cas d’usage ;
- `Performance.Infrastructure` : persistance JSON atomique et migration React ;
- `Performance.App` : hôte MAUI et interface Blazor ;
- `Performance.Application.UnitTests` et `Performance.Infrastructure.UnitTests` : logique métier, catalogue et conversions en mémoire ;
- `Performance.App.UnitTests` : pages, composants et services, avec bUnit pour les composants Razor ;
- `Performance.Infrastructure.IntegrationTests` : sauvegarde et réouverture sur de vrais fichiers JSON temporaires.

L’interface est embarquée dans l’application : l’APK est autonome, utilisable hors ligne et sans hébergement. L’ancienne implémentation React est conservée dans [legacy/web](../legacy/web/README.md).

## Prérequis

- .NET 10 SDK
- workload MAUI Android (`dotnet workload install maui-android`)
- JDK et Android SDK avec plateforme/build-tools 36 ; téléphone Android API 23 minimum

## Compilation Android

Depuis ce dossier :

```sh
dotnet workload restore Performance.slnx
dotnet build src/Performance.App/Performance.App.csproj -f net10.0-android
dotnet test tests/Performance.Application.UnitTests/Performance.Application.UnitTests.csproj
dotnet test tests/Performance.Infrastructure.UnitTests/Performance.Infrastructure.UnitTests.csproj
dotnet test tests/Performance.Infrastructure.IntegrationTests/Performance.Infrastructure.IntegrationTests.csproj
dotnet test tests/Performance.App.UnitTests/Performance.App.UnitTests.csproj
```

Les pages **Programmes**, **Exercices**, **Progrès**, **Historique** et **Réglages** sont fonctionnelles. Le catalogue embarqué reprend 75 identifiants d’exercices, 11 programmes et leurs 44 variantes proposées. Les séries sont enregistrées localement ; une séance inachevée reprend à la réouverture. La fin de séance conserve les séries saisies dans chaque salle.

L’import accepte l’ancien tableau d’historique, la sauvegarde React complète `schemaVersion: 2` et les sauvegardes Android `schemaVersion: 1`. Une prévisualisation et une confirmation précèdent le remplacement des données. L’export Android inclut la séance en cours.

La version React doit être utilisée pour générer une sauvegarde complète avant toute désinstallation. L’APK n’accède pas directement au `localStorage` de la PWA : le transfert passe volontairement par le fichier JSON exporté.

Voir [la génération et les essais Android](README-ANDROID.md) pour l’APK signé et la vérification sur téléphone.

## Conventions de solution et de tests

Ouvrir `Performance.slnx` : les quatre projets applicatifs sont sous `src`, les tests sous `tests/UnitTests` et `tests/IntegrationTests`. Les dépendances restent orientées vers Domain et Application.

Les noms de projets et namespaces suivent `Performance.<Couche>.UnitTests` ou `Performance.<Couche>.IntegrationTests`. Les dossiers de tests reflètent les sources : `Pages`, `Components` et `Services` pour l’interface ; `Setup` contient le routeur utilisé par les tests. Les classes indiquent leur sujet via `TestSubject` ; les tests d’intégration portent le suffixe `Test`.

`Performance.App.UnitTests` compile les fichiers Razor et services portables liés depuis l’application. Son `RootNamespace` reste `Performance.App` pour conserver les mêmes types de composants que sur Android ; les classes de tests ont leurs propres namespaces `Performance.App.UnitTests.*`. Il peut être exécuté sans émulateur ni workload Android. Les essais du WebView, des permissions et de l’APK restent dans la CI et la recette Android.
