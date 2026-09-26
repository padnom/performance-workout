# Performance

Application mobile-first de suivi de musculation. La PWA React reste disponible pendant la migration vers une application Android autonome en .NET MAUI Blazor Hybrid.

## Fonctionnalités

- programmes Push, Pull, Jambes, Upper et séances ciblées ;
- variantes de séances et construction de la semaine ;
- suivi des charges et répétitions sur quatre séries ;
- historique et tableau de progression ;
- exercices personnalisés, tags et machines par salle ;
- reprise automatique de la séance en cours ;
- fonctionnement hors connexion après la première ouverture.

## Stack actuelle

- React 19 et TypeScript ;
- Vinext / Vite ;
- hébergement ChatGPT Sites ;
- stockage local dans le navigateur du téléphone ;
- manifeste PWA et service worker.

## Développement

Prérequis : Node.js 22 ou supérieur.

```bash
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

La version Android utilise .NET MAUI Blazor Hybrid. Son interface et son stockage sont intégrés à l’APK : aucun hébergement n’est requis pour l’utiliser. Le projet se trouve dans `dotnet/` et la CI produit un APK Debug téléchargeable depuis GitHub Actions.

- `Performance.Domain` : règles et modèles métier ;
- `Performance.Application` : cas d’usage et abstractions ;
- `Performance.Infrastructure` : stockage et migration des données React ;
- `Performance.App` : interface MAUI Blazor Hybrid ;
- tests xUnit pour les couches Application et Infrastructure.

## Évolution .NET

Une API ASP.NET Core pourra être ajoutée ultérieurement dans `backend/` pour les comptes, la synchronisation multi-appareils et le partage. Elle restera facultative pour l’utilisation locale de l’APK.

## Structure cible

```text
app/                  application web
public/               manifeste PWA et icônes
dotnet/               solution Clean Architecture et application MAUI
backend/              API ASP.NET Core optionnelle (à venir)
.github/workflows/    intégration continue
```
