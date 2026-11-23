# Dofus Farm Assistant

Application Windows x64 de surveillance passive du client Dofus. Elle ne réalise aucune action de jeu, ne lit pas la mémoire du jeu, n’injecte rien et ne contourne aucune protection Ankama.

## Choix techniques

- C# et WinForms sur .NET 8 : application Windows native, publiée en EXE autonome.
- Surveillance par l’API standard des processus Windows (`Process`), sans interaction avec le jeu.
- Historique en base SQLite et préférences JSON sous `%LOCALAPPDATA%\DofusFarmAssistant` ; mot de passe distant haché avec PBKDF2-SHA256 et mot de passe SMTP protégé par DPAPI pour l’utilisateur Windows courant.
- Petit serveur HTTP lié exclusivement à `127.0.0.1`; Tailscale Serve peut fournir un accès privé TLS sans ouverture de port sur le routeur.

## Compilation et livrable

Le workflow `.github/workflows/build-windows.yml` compile sur un runner Windows `windows-latest`, vérifie l’EXE, crée le dossier portable, produit `DofusFarmAssistant_Windows.zip` et le publie comme artefact GitHub Actions pendant 30 jours. Le déclenchement manuel est disponible dans l’onglet **Actions** de GitHub, bouton **Run workflow**. Le push sur `main` ou `master` lance aussi la compilation.

Dans l’artefact `DofusFarmAssistant-Windows-x64`, le dossier `portable` contient `DofusFarmAssistant.exe` et le guide ; l’archive ZIP se trouve à côté.

## Installation

1. Téléchargez l’artefact de la dernière exécution réussie du workflow Windows.
2. Transférez `DofusFarmAssistant_Windows.zip` vers le PC Windows, extrayez-le dans un dossier et lancez `DofusFarmAssistant.exe`.
3. Windows peut afficher une confirmation SmartScreen pour une application non signée. Vérifiez que le fichier provient bien de l’artefact de votre dépôt.
4. L’application n’exige pas Python, Node.js ni Visual Studio.

## Utilisation

- Dans **Paramètres**, saisissez le nom du processus réellement visible dans le Gestionnaire des tâches, sans `.exe`. La valeur proposée `Dofus` est un choix par défaut et peut nécessiter adaptation selon l’édition/launcher.
- Cliquez **Démarrer la surveillance**. Les états sont Jeu ouvert, Jeu fermé et Processus bloqué.
- « Jeu fermé » signifie uniquement que le processus surveillé n’existe plus. Cela ne prouve pas une déconnexion du compte.
- Dofus n’expose pas ici d’indicateur fiable et documenté de déconnexion. L’état « déconnexion confirmée » n’est donc pas inventé ; aucune alerte de déconnexion potentielle n’est émise sans signal observable.
- La capture prend l’écran principal Windows de la session utilisateur. Les captures sont des fichiers PNG conservés localement. Réglez leur intervalle, la durée et la limite de stockage dans **Paramètres**.
- L’historique et les paramètres sont enregistrés sous `%LOCALAPPDATA%\DofusFarmAssistant`.
- Pour SMTP : configurez hôte, port TLS, identifiant, mot de passe d’application si requis, destinataire, puis utilisez le bouton de test. Le mot de passe SMTP est chiffré avec DPAPI et lisible uniquement par le même compte Windows.
- Les réglages doivent être enregistrés pour persister. L’option de démarrage Windows utilise la clé Run du profil utilisateur.

## Accès privé depuis iPhone

1. Installez Tailscale sur le PC et l’iPhone, et connectez-les au même tailnet.
2. Dans l’application, onglet **Sécurité**, définissez un mot de passe d’au moins 14 caractères et démarrez le tableau de bord.
3. Sur le PC, ouvrez Terminal et lancez `tailscale serve --bg http://127.0.0.1:8765`. Tailscale affiche alors l’adresse HTTPS privée `https://<nom-du-PC>.<tailnet>.ts.net`. Cette commande utilise Serve, réservé au tailnet ; ne lancez pas Funnel. Si votre version demande d’activer HTTPS pour le tailnet, suivez le lien d’activation affiché par Tailscale.
4. Sur l’iPhone, ouvrez dans Safari l’URL HTTPS privée du PC affichée par Tailscale et connectez-vous avec le mot de passe distant. Vous pourrez lire l’état, l’historique récent, les captures et demander une capture.
5. Ne configurez aucune redirection de port sur le routeur. Le serveur de l’application écoute seulement sur l’interface loopback. Pour désactiver le relais plus tard, lancez `tailscale serve off`.

Le tableau de bord distant dépend de Tailscale Serve et d’un PC allumé, connecté, avec l’application ouverte. La perte de communication avec le PC ne peut pas être signalée par le PC lui-même lorsqu’il est hors ligne ; une alerte externe fiable nécessiterait un service tiers disponible.

## Sécurité et limites connues

- Le tableau de bord utilise une authentification par mot de passe et un cookie HttpOnly/Secure/SameSite ; choisissez un mot de passe unique et long.
- SMTP est facultatif. Twilio/SMS n’est pas intégré dans cette version, afin de ne pas stocker de secrets d’API non testés. Les tarifs SMS dépendent du pays, du numéro et du fournisseur : aucun coût fixe n’est inclus.
- L’état « processus bloqué » dépend de la réponse Windows du processus et peut ne pas être disponible pour tous les types de processus ou droits d’accès.
- Aucun processus Dofus réel n’était disponible dans l’environnement de développement pour valider les noms de processus et la détection en situation de jeu.
- Le projet n’est pas signé numériquement. Une signature de code demanderait un certificat de signature que le dépôt ne fournit pas.

## Mise à jour

Téléchargez le nouvel artefact Windows et remplacez l’ancien dossier portable par son contenu. Les données et paramètres restent dans `%LOCALAPPDATA%\DofusFarmAssistant`.

## Désinstallation

Fermez l’application, supprimez le dossier portable, puis supprimez `%LOCALAPPDATA%\DofusFarmAssistant`. Si le démarrage automatique est activé, désactivez-le avant fermeture depuis les paramètres ; sinon supprimez l’entrée `DofusFarmAssistant` dans `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Désactivez également Tailscale Serve si vous l’avez configuré.

## État des vérifications

Le code source et le workflow sont présents. Cet environnement dispose du runtime .NET 8 mais aucun SDK .NET, n’est connecté à un dépôt GitHub et ne donne pas accès à un EXE compilé. Le workflow n’a donc pas été exécuté ici ; aucun EXE ni ZIP compilé n’est annoncé comme livré. Après connexion du projet à GitHub, lancez le workflow pour produire et télécharger l’artefact.
