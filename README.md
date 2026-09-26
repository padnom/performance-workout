# Performance

Application mobile-first de suivi de musculation, installable en PWA.

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

Les séances, records et exercices personnalisés sont actuellement stockés sur l’appareil avec `localStorage`. Effacer les données du navigateur peut donc supprimer l’historique. Un export/import versionné doit être ajouté avant la diffusion de l’APK.

## Android

La première version Android prévue utilisera une Trusted Web Activity afin de réutiliser la PWA et le domaine existants. Le projet Android sera placé dans `android/` et produira :

- un APK pour installation directe ;
- un AAB pour une éventuelle publication sur Google Play.

## Évolution .NET

Une API ASP.NET Core pourra être ajoutée ultérieurement dans `backend/` pour les comptes, la synchronisation multi-appareils et le partage. Une réécriture MAUI/Blazor Hybrid n’est envisagée que si des fonctionnalités natives deviennent centrales.

## Structure cible

```text
app/                  application web
public/               manifeste PWA et icônes
android/              projet Android TWA (à venir)
backend/              API ASP.NET Core optionnelle (à venir)
.github/workflows/    intégration continue
```
