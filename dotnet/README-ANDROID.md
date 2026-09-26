# Construire l’APK Android

Ce dépôt contient actuellement l’application web. Le workflow Android devient actif dès qu’un projet .NET MAUI Blazor Hybrid est ajouté sous `dotnet/`. Il prend en charge une solution nommée `dotnet/Performance.sln` et repère le projet MAUI à partir de son fichier `.csproj`.

## Structure conseillée

```text
dotnet/
  Performance.sln
  src/
    Performance.Domain/
    Performance.Application/
    Performance.Infrastructure/
    Performance.App/
      Performance.App.csproj
  tests/
    Performance.Application.Tests/
    Performance.Infrastructure.Tests/
```

Le projet MAUI doit cibler `net10.0-android` (ou inclure cette cible dans `<TargetFrameworks>`) et être configuré en Blazor Hybrid, généralement avec `<UseMaui>true</UseMaui>` et une référence à `Microsoft.AspNetCore.Components.WebView.Maui`. Le workflow vérifie explicitement cette cible, cohérente avec le SDK .NET 10 qu’il installe.

## Génération locale

Prérequis : SDK .NET 10, workload Android MAUI, JDK 17 et Android SDK. Depuis la racine du dépôt :

```bash
dotnet workload install maui-android
dotnet restore dotnet/Performance.sln
dotnet test dotnet/Performance.sln --configuration Debug
dotnet build dotnet/src/Performance.App/Performance.App.csproj \
  --configuration Debug \
  --framework net10.0-android \
  -p:AndroidPackageFormats=apk
```

L’APK se trouve sous `dotnet/src/Performance.App/bin/Debug/net10.0-android/`. Le nom exact dépend du nom d’assembly et de la version du SDK.

## APK de diagnostic dans GitHub Actions

Le workflow `.github/workflows/android.yml` compile l’application en configuration `Debug` et met l’APK à disposition dans l’onglet **Actions → exécution du workflow → Artifacts**. Il est lancé pour les changements sous `dotnet/`, pour le workflow lui-même, et manuellement via **Run workflow**.

La compilation Debug utilise la clé de débogage Android générée localement par la chaîne de compilation. Aucun secret de signature n’est nécessaire ni lu par ce workflow. L’APK est destiné à l’installation et aux essais, pas à Google Play ni à une diffusion Release. Un futur paquet Release devra utiliser une clé de signature dédiée stockée dans les secrets GitHub, avec un workflow séparé et des protections adaptées.

Le dépôt peut rester privé : le workflow demande uniquement `contents: read`, ne publie pas l’APK dans une Release et conserve l’artefact GitHub pendant sept jours. Seules les personnes ayant accès au dépôt peuvent télécharger l’artefact.

Tant qu’aucun projet MAUI Android n’existe sous `dotnet/`, le workflow affiche un message informatif et termine sans échec. Une fois le `.csproj` ajouté, il choisit celui qui déclare `UseMaui` ou un TFM Android. Le projet doit déclarer littéralement `net10.0-android`, afin que la cible soit détectable de manière fiable en CI.

## Données utilisateur

Un APK autonome ne dépend pas de l’hébergement du site, mais il ne peut pas lire directement le `localStorage` isolé de la PWA. La version React produit donc une sauvegarde JSON versionnée, acceptée par l’adaptateur de migration MAUI. Il faut exporter ce fichier avant d’abandonner l’ancienne application, puis vérifier la reprise des historiques sur l’APK. Une synchronisation entre appareils nécessitera ensuite un service distant (par exemple ASP.NET Core) ; l’installation locale seule ne la fournit pas.
