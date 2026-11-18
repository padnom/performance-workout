# Construire l’APK Android

L’application Android est une **.NET MAUI Blazor Hybrid** en C#. Son interface, son catalogue et ses données sont embarqués : aucun hébergement web ni serveur n’est nécessaire pour s’entraîner. Les liens externes vers les mouvements nécessitent Internet.

## Structure

```text
dotnet/
  Performance.slnx
  src/
    Performance.Domain/
    Performance.Application/
    Performance.Infrastructure/
    Performance.App/
      Performance.App.csproj
  tests/
    Performance.Application.UnitTests/
    Performance.Infrastructure.UnitTests/
    Performance.Infrastructure.IntegrationTests/
    Performance.App.UnitTests/
```

Le projet `Performance.App` cible `net10.0-android`. Les tests de composants compilent les mêmes pages Razor sans lancer Android ; ils complètent les tests de logique et de persistance.

## Génération locale

Prérequis : SDK .NET 10, workload Android MAUI, JDK (21 recommandé) et Android SDK avec la plateforme et les build-tools 36. L’application cible les téléphones Android API 23 ou supérieure. Depuis la racine du dépôt :

```bash
dotnet workload install maui-android
dotnet restore dotnet/Performance.slnx
dotnet test dotnet/tests/Performance.Application.UnitTests/Performance.Application.UnitTests.csproj
dotnet test dotnet/tests/Performance.Infrastructure.UnitTests/Performance.Infrastructure.UnitTests.csproj
dotnet test dotnet/tests/Performance.Infrastructure.IntegrationTests/Performance.Infrastructure.IntegrationTests.csproj
dotnet test dotnet/tests/Performance.App.UnitTests/Performance.App.UnitTests.csproj
dotnet build dotnet/src/Performance.App/Performance.App.csproj \
  --configuration Debug \
  --framework net10.0-android \
  -p:AndroidPackageFormats=apk
```

L’APK installable est `fr.padnom.performance-Signed.apk`, sous `dotnet/src/Performance.App/bin/Debug/net10.0-android/`. Un éventuel réglage MSBuild global peut rediriger ce dossier ; le journal de compilation indique le chemin effectif. Utiliser le fichier signé.

`EmbedAssembliesIntoApk=true` est défini dans le projet pour inclure le code C# même en Debug. Sans ce réglage, le Fast Deployment peut produire un APK signé qui plante lors d’une installation manuelle avec `No assemblies found`.

Vérifier le paquet, puis le lancer sur un appareil ou émulateur connecté :

```bash
dotnet run --project dotnet/tools/Performance.ReleaseTools -- verify-debug-apk chemin/vers/fr.padnom.performance-Signed.apk
dotnet run --project dotnet/src/Performance.App/Performance.App.csproj \
  --framework net10.0-android --device emulator-5554
```

Pour valider une livraison autonome, installer aussi directement le fichier avec `adb install` sur un appareil de test propre. Un lancement par l’IDE peut masquer des assemblies absentes de l’APK en les déployant séparément.

## APK optimisé pour installation locale

Pour les essais de navigation sur téléphone, générer un paquet optimisé :

```bash
dotnet build dotnet/src/Performance.App/Performance.App.csproj \
  --configuration Release \
  --framework net10.0-android \
  -p:AndroidPackageFormats=apk \
  -p:RunAOTCompilation=true \
  -p:PublishTrimmed=true
```

Le fichier signé se trouve sous `dotnet/src/Performance.App/bin/Release/net10.0-android/`. La version cible est **1.5.2, code Android 10**. Le certificat local actuel est conservé : utiliser le même environnement de signature pour installer par-dessus l’application existante et conserver son stockage. Ne pas désinstaller pour effectuer cette mise à jour.

Les contrôles de livraison sont écrits en C# dans `dotnet/tools/Performance.ReleaseTools` (outil console `performance-release`, testé par `dotnet/tests/Performance.ReleaseTools.UnitTests`). `verify-debug-apk` reste disponible pour le format Debug. Le contrôle Release vérifie les bibliothèques AOT et le stockage des assemblies pour arm64-v8a/x86_64, l’interface embarquée, l’intégrité du ZIP, la signature Android, l’absence du drapeau Debug et la concordance des versions :

```bash
dotnet run --project dotnet/tools/Performance.ReleaseTools -- verify-release-apk \
  dotnet/src/Performance.App/bin/Release/net10.0-android/fr.padnom.performance-Signed.apk \
  --project dotnet/src/Performance.App/Performance.App.csproj \
  --build-tools "$ANDROID_HOME/build-tools/36.0.0"
dotnet run --project dotnet/tools/Performance.ReleaseTools -- smoke-android \
  dotnet/src/Performance.App/bin/Release/net10.0-android/fr.padnom.performance-Signed.apk \
  --output /tmp/performance-android-smoke
```

Le second contrôle nécessite un émulateur/appareil de test connecté. Il installe avec `adb install -r`, lance directement l’APK, attend le rendu de l’accueil et conserve une capture, l’arbre accessible et les journaux de démarrage. Il n’efface pas les données mais arrête une éventuelle instance en cours ; utiliser un appareil de test. Ce contrôle de lancement ne remplace pas les essais d’une séance complète ni la mesure de fluidité sur téléphone.

## Release dans GitHub Actions

Le workflow `.github/workflows/android.yml` exécute les quatre suites .NET en **Release**, les tests des contrôles de livraison, puis construit un APK **Release AOT et trimming** pour les deux architectures. Il vérifie le paquet et le lance sur un émulateur API 36/x86_64. Il se déclenche pour les changements Android, les pull requests, les tags `android-v*` et les exécutions manuelles. Les preuves de lancement sont téléchargeables dans les artefacts.

Le job de vérification utilise un certificat de diagnostic jetable généré sur le runner, y compris pour les pull requests ; il ne lit aucun secret. Son artefact `performance-release-diagnostic-not-an-update` sert aux essais sur une installation de test et **ne permet pas de mettre à jour l’installation de ton téléphone**.

La signature de mise à jour est un job séparé, exécuté après les vérifications uniquement pour un tag `android-v*` ou un lancement manuel avec `distribute=true`. Configurer dans l’environnement GitHub **android-release** les secrets `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS` et `ANDROID_KEY_PASSWORD`, provenant du **certificat déjà utilisé pour l’application installée**. Ne pas générer une nouvelle clé pour cette mise à jour. Les mots de passe restent dans l’environnement, le keystore temporaire est supprimé et aucune clé n’est incluse dans les artefacts. Sans ces quatre secrets, le job de distribution échoue explicitement ; la validation de diagnostic reste disponible.

L’artefact `performance-release-update` n’est distribué qu’après vérification de signature, contenu, version et concordance avec l’empreinte publique du certificat actuel (`dotnet/tools/android-signing-certificate.sha256`). Une clé différente provoque un échec avant diffusion. Ce fichier ne contient aucune clé privée. Le job signe le même paquet optimisé déjà testé ; il ne recompilera pas un paquet différent. Protéger l’environnement et les tags de livraison selon les droits souhaités dans GitHub. Le job publie ensuite la GitHub Release lue par la mise à jour intégrée ; aucune publication Google Play n’est effectuée. En local, sans configuration de signature dans le projet, .NET signe avec le keystore de débogage du PC (`~/.local/share/Xamarin/Mono for Android/debug.keystore`, alias `androiddebugkey`, mots de passe `android`) : ce sont ces valeurs qu’il faut copier dans les secrets.

Le dépôt doit être public : la mise à jour intégrée lit `releases/latest` sans authentification, et un dépôt privé lui répond 404. Le job de vérification demande uniquement `contents: read`. Les artefacts de diagnostic/preuves sont conservés sept jours, celui de mise à jour quatorze jours. Seules les personnes ayant accès au dépôt peuvent télécharger les artefacts.

La version est déclarée dans le projet et n’est jamais réécrite automatiquement par une compilation. `verify-version` refuse toute régression et impose un code Android supérieur pour une distribution. En vérification ordinaire, une version inchangée est autorisée. Le job de distribution compare au précédent tag `android-v*` ; pour une livraison manuelle, `previous_release_ref` permet de désigner le dernier commit/tag effectivement distribué. Sans tag initial, la référence de début du dépôt correspond à la première version Android 1.0/code 1. Taguer chaque livraison distribuée pour maintenir cette référence ; ne pas réutiliser le code 2 pour la suivante.

Le workflow doit être poussé pour s’exécuter sur GitHub. Les vérifications locales ne prouvent ni l’exécution CI ni la configuration des secrets GitHub.

## Données utilisateur

Un APK autonome ne dépend pas de l’hébergement du site, mais il ne peut pas lire directement le `localStorage` isolé de la PWA. La version React produit donc une sauvegarde JSON versionnée, acceptée par l’adaptateur de migration MAUI. Il faut exporter ce fichier avant d’abandonner l’ancienne application, puis vérifier la reprise des historiques sur l’APK. Une synchronisation entre appareils nécessitera ensuite un service distant (par exemple ASP.NET Core) ; l’installation locale seule ne la fournit pas.

## Essai sur téléphone

Le lancement après installation propre, `dotnet run` et le redémarrage hors ligne ont été vérifiés sur un émulateur Android API 36. Les essais sur ton téléphone restent nécessaires. Installer l’APK puis vérifier :

1. En mode avion, ouvrir les programmes et démarrer explicitement une séance.
2. Saisir charge et répétitions ; fermer complètement l’application et la rouvrir pour retrouver la séance. Le formulaire n’affiche plus le RIR, mais les anciennes valeurs restent conservées dans les données.
3. Changer de salle, saisir d’autres séries puis terminer : vérifier l’historique et les progrès par salle.
4. Importer la sauvegarde React et confirmer seulement après lecture du récapitulatif.
5. Exporter une sauvegarde Android, puis la réimporter et vérifier les données.
6. Vérifier le clavier, le bouton retour, le défilement et le minuteur pendant l’utilisation réelle.
7. Pendant une séance, saisir les répétitions : vérifier le repos automatique, changer sa durée, utiliser « +30 s », fermer puis rouvrir l’application avant son échéance et vérifier sa reprise.
8. Ouvrir « Historique de mes séances » pendant la séance, développer les séries puis fermer la fenêtre ; vérifier que la saisie et les records par salle sont conservés.
9. Comparer les changements d’écran entre Accueil, Programmes, Progrès, Historique et Plus avec l’APK précédent. Plus donne accès à Exercices et Réglages. Aucune mesure de latence sur téléphone n’a encore été effectuée.
10. Dès la confirmation du démarrage, vérifier la barre compacte « Temps total » sous l’en-tête ; elle doit rester visible dès le premier défilement et avec le clavier ouvert. Le compteur doit reprendre la durée réelle après réouverture. Aucune heure de démarrage n’est affichée.
11. Dans Programmes, choisir Classiques / Ciblées, une catégorie puis une variante A/B/C/D : le détail de ses exercices s’affiche en dessous. Le bouton « + » de chaque carte ajoute la séance à la semaine, y compris deux fois la même, sans lancer une séance. Vérifier « Programme actif / Ma semaine », déplacer avec ↑ ↓ et retirer une occurrence avec ×, fermer puis rouvrir l’application. « Démarrer » dans le détail demande confirmation ; toucher une séance de la semaine ouvre son aperçu. Les programmes importés personnalisés restent accessibles sous Personnalisés. Pour retrouver la semaine de l’ancienne PWA, importer sa sauvegarde complète depuis Réglages.
12. Dans Accueil, vérifier les séances et séries des sept derniers jours glissants et les cartes de la semaine dans le même ordre que Programmes. Démarrer une séance, revenir à Accueil puis utiliser « Reprendre la séance » : retrouver les séries et la durée initiale. Fermer et rouvrir l’application ; le lien de reprise reste disponible. Après terminaison, l’accueil met ses statistiques à jour. Les compteurs ne comptent pas le brouillon actif.

La désinstallation efface le stockage privé de l’application : exporter ses données auparavant.

La **Vibration de fin de repos** est **optionnelle et désactivée par défaut**, au premier plan comme en arrière-plan. Une seule vibration de 200 ms est prévue, sans son ni fenêtre intrusive. Une notification silencieuse permet de revenir à la séance. Dans Réglages, son activation demande les autorisations nécessaires ; aucun prompt au lancement. Le rappel natif sert à déclencher cette vibration écran éteint, et le statut précise si les autorisations sont bloquées ou si Android peut retarder le rappel. Sur téléphone, tester l’autorisation acceptée/refusée, l’écran verrouillé, le passage en arrière-plan, « +30 s », l’arrêt, la fin de séance et la désactivation du réglage ; vérifier qu’aucun ancien rappel ou double vibration n’arrive. Tester aussi Retour avec une fenêtre ouverte, la reprise après interruption, le clavier et la barre de séance pendant le défilement. Ces essais physiques restent à réaliser et ne sont pas couverts par le simple lancement automatisé.

Pendant une séance, chaque carte propose Remplacer, Modifier et Retirer ; la recherche permet aussi de créer un exercice. Les changements sont sauvegardés avant confirmation. Modifier le nombre de séries conserve les premières lignes et ajoute des lignes vides ; les brouillons et records restent séparés par salle. Une carte peut être réduite manuellement avec son résumé de séries. Le temps total, le repos, le compteur de séries et Terminer restent accessibles pendant le défilement et avec le clavier.

Progrès reprend le bilan du site sur les **28 derniers jours** : séances, séries saisies, durée moyenne, programmes réalisés et zones musculaires principales. Les séries avec une charge ou des répétitions sont comptées ; le RIR seul ne compte pas. La courbe par exercice utilise **tout l’historique de la salle choisie**, avec début, variation et record. Toucher un point affiche sa date et sa charge ; les valeurs datées restent accessibles sous le graphique. Avant deux séances, un message indique comment faire apparaître la courbe. Les records de toutes les salles restent consultables dans la section dépliable. Aucun accès réseau n’est nécessaire.

La barre de séance est désormais **compacte et fixée en haut du défilement**, sous l’en-tête : temps de séance, séries, Terminer et repos (+30 s / arrêt). Les totaux de poids soulevé ne sont plus affichés : l’accueil montre les séries sur sept jours, l’historique montre les exercices et les séries, et Progrès montre les charges/records et leur évolution. La durée d’une séance et sa moyenne restent disponibles, sans cumul de temps passé en salle.

## Programmes par salle et partage — 1.2.0

Dans Programmes, « Ton programme » propose Push, Pull, Jambes et Upper pour One Air ou Basic-Fit. Les exercices principaux sont en 4×10, avec des séries, répétitions et repos modifiables. One Air utilise les machines déjà cartographiées ; Basic-Fit propose du matériel standard à vérifier dans la salle utilisée. « Utiliser cette semaine » demande confirmation avant de remplacer la semaine. Les anciens programmes restent sous « Autres programmes » et les programmes importés sous « Personnalisés ».

Le choix de salle au démarrage adapte les séances recommandées à cette salle. Changer la salle dans Réglages ne transforme pas une séance en cours. Les anciennes semaines et séances personnalisées sont conservées ; aucune charge n’est transférée automatiquement entre machines ou salles.

Deux exercices d’abdos optionnels apparaissent en fin de séance : machine abdominale et reverse crunch à One Air, crunch à la poulie et reverse crunch à Basic-Fit. Ils sont proposés en 2×10–15 avec 60 secondes de repos, sans être comptés avant leur ajout volontaire. Après ajout, ils se modifient et s’enregistrent comme les autres exercices. Ils ne sont pas ajoutés une seconde fois après réouverture.

« Partager des programmes » exporte la semaine ou les programmes cochés dans un JSON `performance-programs`, version 1, avec les définitions d’exercices et les consignes. Ce fichier exclut historique, charges personnelles, séance en cours, réglages et diagnostics. Android permet de choisir une destination ou une application de partage. L’import affiche un aperçu puis ajoute des copies après confirmation, sans remplacer les données existantes. Un exercice portant le même identifiant mais une définition différente reçoit un nouvel identifiant.

La sauvegarde complète de Réglages reste distincte et conserve son rôle pour sauvegarder ou migrer les données personnelles. Les fichiers de partage de programmes sont à importer depuis Programmes.

## Création et favoris — 1.2.1

Dans Programmes, « Créer un programme » ouvre un brouillon local : nom, description, salle et séances. Ajouter les exercices depuis la recherche, puis régler les séries, répétitions et repos. Les séances et exercices peuvent être déplacés ou retirés avant l’enregistrement. Annuler ou utiliser Retour ferme le brouillon sans modifier les données ; une erreur d’écriture conserve la saisie pour réessayer.

Les programmes créés apparaissent sous « Mes programmes ». Leurs séances utilisent les mêmes actions d’aperçu, de démarrage et d’ajout à la semaine. Le partage JSON inclut les consignes et définitions d’exercices nécessaires.

Les fichiers de partage restent limités à 1 Mio. Un programme exceptionnellement volumineux peut dépasser cette limite ; l’export affiche alors une erreur et conserve les données locales.

Une étoile ajoute ou retire un programme des Favoris, y compris les programmes intégrés et importés. Le choix « Ajouter aux favoris » dans l’éditeur est enregistré avec le nouveau programme. Les favoris sont personnels : conservés dans la sauvegarde complète, ils sont exclus des fichiers partagés avec les amis.

Sur téléphone, vérifier la création de deux séances, l’ordre des exercices, le clavier, Annuler/Retour et la réouverture après enregistrement. Marquer un programme comme favori, redémarrer l’application et vérifier sa présence, puis exporter/importer le programme sur une autre installation : il doit être ajouté comme copie sans reprendre le favori personnel.

## Partage de séances — 1.3.0

Une séance de l’historique (séance libre comprise) ou une séance de programme ouverte avant démarrage peut être exportée en JSON `performance-session`, version 1, ou partagée en texte lisible. Le fichier contient uniquement la structure : exercices, ordre, séries, répétitions cibles, repos et salle. Il exclut charges, répétitions réalisées, RIR et historique. Depuis l’historique, le nombre de séries est celui réellement effectué.

« Refaire cette séance » (Historique) relance une séance terminée avec les mêmes exercices, dans le même ordre et avec le même nombre de séries, via la fenêtre de démarrage habituelle (salle présélectionnée sur celle utilisée). Les charges sont pré-remplies avec la dernière performance. Les séances terminées conservent désormais leur nom dans l’historique.

« Importer une séance » dans Programmes, ou l’ouverture d’un fichier `.json` reçu (WhatsApp, gestionnaire de fichiers), affiche un aperçu puis ajoute une copie dans le programme personnel « Mes séances », créé au besoin et limité à 100 séances. « Créer une séance » ouvre l’éditeur pour une seule séance enregistrée au même endroit. Un fichier reçu pendant une séance en cours attend la prochaine visite de Programmes.

Sur téléphone, vérifier : envoi à soi-même sur WhatsApp du fichier et du texte ; ouverture du fichier reçu application fermée puis ouverte ; refus d’un JSON quelconque avec message ; démarrage et ajout à la semaine d’une séance de « Mes séances » ; mise à jour par-dessus 1.2.1 sans perte de données.

## Mise à jour automatique et thème par salle — 1.4.0

**Nouveautés.** Au premier lancement d’une nouvelle version, une fenêtre « Nouveautés de la version X » résume les changements (rédigés dans `ReleaseNotes.cs`, donc disponibles hors ligne). Elle ne s’affiche qu’une fois par version et jamais pendant une séance. Le même texte sert de description à la GitHub Release.

**Mise à jour.** Au démarrage puis au retour au premier plan (au plus toutes les 6 h), l’application interroge `api.github.com/repos/padnom/performance-workout/releases/latest`. Si la version publiée est plus récente que la version installée, un bandeau « Nouvelle version » propose « Mettre à jour » ou « Plus tard » ; Réglages › Mises à jour permet aussi une recherche manuelle. L’APK est téléchargé dans le cache, sa taille et son empreinte SHA-256 (champ `digest` de GitHub) sont vérifiées, puis l’installateur Android s’ouvre. Android exige toujours une confirmation : une installation totalement silencieuse n’est pas possible hors Play Store. Au premier usage, Android demande d’autoriser « Performance » à installer des applications ; l’installation reprend au retour dans l’application. Hors connexion, la vérification échoue silencieusement. Permissions ajoutées : `INTERNET`, `REQUEST_INSTALL_PACKAGES`.

**Publier une version.** Ajouter les notes de la version dans `dotnet/src/Performance.Application/ReleaseNotes.cs` (sans elles, les tests et la distribution échouent), augmenter `ApplicationDisplayVersion` et `ApplicationVersion`, puis pousser le tag `android-v<version>` (par exemple `android-v1.4.0`). Le job `distribution` signe l’APK avec le certificat existant et crée la GitHub Release `performance-<version>.apk` lue par l’application. Le tag doit correspondre exactement à la version du projet. Variante sans tag : lancer manuellement le workflow « Android Release APK » avec `distribute` coché ; il crée alors le tag `android-v<version>` et la Release sur le commit choisi. Seul un APK signé avec le même certificat peut s’installer par-dessus l’existant ; l’APK de diagnostic ne convient pas.

**Thème par salle.** La couleur d’accent suit la salle de la séance en cours, sinon celle choisie dans Réglages : rouge et noir pour On Air, orange pour Basic-Fit, vert d’origine pour « Toutes ». Le logo et le nom de la salle s’affichent en haut à droite (`wwwroot/img/gyms/` : logo On Air, monogramme « BF » pour Basic-Fit). La salle s’affiche « On Air » ; la valeur enregistrée reste `One Air` pour garder la compatibilité des données et sauvegardes. Couleurs relevées sur les logos : On Air `#AE1D22` (accent éclairci `#D8232A` pour rester lisible sur fond noir), Basic-Fit `#F37121`. Les couleurs sont des variables CSS dans `wwwroot/css/app.css` (`.app-shell[data-gym=…]`) ; ajouter une salle = un bloc de variables et une entrée dans `GymTheme.For`.

Sur téléphone, vérifier : bandeau après publication d’une release, refus puis octroi de l’autorisation d’installation, installation par-dessus 1.3.0 sans perte de données, absence de bandeau hors connexion ; passage One Air ↔ Basic-Fit dans Réglages, depuis le bouton de salle en haut à droite (hors séance et pendant une séance) et dans la séance en cours.
