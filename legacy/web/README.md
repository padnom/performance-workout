# Performance — référence web archivée

Ancienne application React de suivi de musculation, conservée pour comparer les comportements et les sauvegardes. L’application principale est désormais [l’APK Android](../../README.md). Ce dossier ne participe pas à la fabrication de l’APK ; son ancien pipeline GitHub est conservé ici comme référence et n’est plus exécuté.

## Fonctionnalités

- programmes Push, Pull, Jambes, Upper et séances ciblées ;
- variantes de séances et construction de la semaine ;
- suivi des charges et répétitions sur quatre séries ;
- historique et tableau de progression ;
- exercices personnalisés, tags et machines par salle ;
- reprise automatique de la séance en cours ;
- fonctionnement hors connexion après la première ouverture.

## Stack historique

- React 19 et TypeScript ;
- Vinext / Vite ;
- hébergement ChatGPT Sites ;
- stockage local dans le navigateur du téléphone ;
- manifeste PWA et service worker.

## Développement

Prérequis : Node.js 22 ou supérieur.

```bash
cd legacy/web
npm ci
npm run dev
```

Compilation :

```bash
npm run build
```

## Données

Les séances, records et exercices personnalisés de la PWA sont stockés sur l’appareil avec `localStorage`. L’onglet Réglages exporte maintenant une sauvegarde JSON complète et versionnée, importable par la nouvelle application MAUI. Effacer les données du navigateur avant cet export peut supprimer l’historique.

## Android

La version Android utilise .NET MAUI Blazor Hybrid. Son interface et son stockage sont intégrés à l’APK : aucun hébergement n’est requis pour l’utiliser. Le projet se trouve dans [dotnet](../../dotnet/README.md) ; [le guide Android](../../dotnet/README-ANDROID.md) décrit les APK Release et leur signature.

- `Performance.Domain` : règles et modèles métier ;
- `Performance.Application` : cas d’usage et abstractions ;
- `Performance.Infrastructure` : stockage et migration des données React ;
- `Performance.App` : interface MAUI Blazor Hybrid ;
- tests xUnit pour les couches Application et Infrastructure.

## Structure de cette référence

```text
app/                  application web
public/               manifeste PWA et icônes
.github/workflows/    ancien pipeline, inactif dans ce sous-dossier
```
