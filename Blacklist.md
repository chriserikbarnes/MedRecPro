# Cloudflare WAF Custom Rule - Bot Probe Path Blocking

- **Generated:** 2026-02-24
- **Source:** `D:\MedRecPro_Host_Failure_Feb_2026.csv` (1,226 unique paths, ~5,200+ hits)

---

## Rule Configuration

| Property | Value |
|---|---|
| **Rule Name** | Block Bot Probe Paths (URI) |
| **Action** | Block |
| **Priority** | Place AFTER your existing bot user-agent rule |

> **NOTE:** This rule complements the existing user-agent bot rule. That rule blocks
> known bot crawlers by User-Agent header. THIS rule blocks vulnerability scanner
> paths regardless of User-Agent, catching bots that spoof or omit their UA.
>
> Safe for MedRecPro (.NET) because no legitimate route contains `.php`, `.env`,
> `/wp-`, `/.git`, `/.aws`, etc.

---

## Expression

Paste into **"Edit expression"** in Cloudflare rule builder:

```
(http.request.uri.path contains ".php") or (http.request.uri.path contains ".env") or (http.request.uri.path contains "/wp-") or (http.request.uri.path contains "wlwmanifest.xml") or (http.request.uri.path contains "/.git") or (http.request.uri.path contains "/.svn") or (http.request.uri.path contains "/.hg/") or (http.request.uri.path contains "/.bzr/") or (http.request.uri.path contains "/.aws") or (http.request.uri.path contains "/.ssh") or (http.request.uri.path contains "/.docker") or (http.request.uri.path contains "/.vscode") or (http.request.uri.path contains "/.idea") or (http.request.uri.path contains "/.circleci") or (http.request.uri.path contains "/.bitbucket") or (http.request.uri.path contains "/.travis") or (http.request.uri.path contains "/.next/") or (http.request.uri.path contains "/.nuxt/") or (http.request.uri.path contains "/.vite/") or (http.request.uri.path contains "/.output/") or (http.request.uri.path contains "/actuator") or (http.request.uri.path contains "/_profiler") or (http.request.uri.path contains "/_ignition") or (http.request.uri.path contains "/__debug") or (http.request.uri.path contains "phpmyadmin") or (http.request.uri.path contains "phpMyAdmin") or (http.request.uri.path contains "phpinfo") or (http.request.uri.path contains "/debug/default/view") or (http.request.uri.path contains ".htaccess") or (http.request.uri.path contains ".sql") or (http.request.uri.path contains "docker-compose") or (http.request.uri.path contains "serverless.yml") or (http.request.uri.path contains "private.key") or (http.request.uri.path contains "backup.zip") or (http.request.uri.path contains "backup.tar.gz") or (http.request.uri.path contains "secrets.yml") or (http.request.uri.path contains "aws-secret") or (http.request.uri.path contains "aws-credentials") or (http.request.uri.path contains ".npmrc") or (http.request.uri.path contains ".pypirc") or (http.request.uri.path contains ".boto") or (http.request.uri.path contains "composer.lock") or (http.request.uri.path contains "/graphql") or (http.request.uri.path contains "/graphiql") or (http.request.uri.path contains "server-info") or (http.request.uri.path contains "server-status") or (http.request.uri.path contains "/administrator/") or (http.request.uri.path contains "/feed/") or (http.request.uri.path contains "/package/dynamic_js/")
```

---

## Coverage Breakdown

| Pattern | Catches | Est. Hits |
|---|---|---|
| `contains ".php"` | All PHP file probes: xmlrpc, wp-login, phpinfo, shells, admin.php, adminer, style.php, postnews.php, all .php3/.php4/.php5 variants | ~600+ |
| `contains ".env"` | All env file harvesting: /.env, sendgrid.env, env.js, env.json, plus 700+ deep path .env variants (/backend/.env, /docker/.env, etc.) | ~3000+ |
| `contains "/wp-"` | WordPress: wp-admin, wp-login, wp-content, wp-includes, wp-config (non-.php paths like wp-config.old) | ~200+ |
| `contains "wlwmanifest.xml"` | Windows Live Writer manifest probes across /blog/, /web/, /shop/, etc. | ~500+ |
| `contains "/.git"` | Git exposure + GitHub + GitLab + .gitignore (all contain "/.git") | ~100+ |
| `contains "/.svn"` | Subversion exposure | ~4 |
| `contains "/.hg/"` | Mercurial exposure | ~4 |
| `contains "/.bzr/"` | Bazaar exposure | ~4 |
| `contains "/.aws"` | AWS credentials/config | ~40+ |
| `contains "/.ssh"` | SSH key exposure | ~8 |
| `contains "/.docker"` | Docker configs + .dockerignore | ~20+ |
| `contains "/.vscode"` | VS Code settings/SFTP config | ~14+ |
| `contains "/.idea"` | JetBrains IDE config | ~4 |
| `contains "/.circleci"` | CircleCI config | ~10 |
| `contains "/.bitbucket"` | Bitbucket pipelines | ~8 |
| `contains "/.travis"` | Travis CI config | ~6 |
| `contains "/.next/"` | Next.js build artifacts | ~30+ |
| `contains "/.nuxt/"` | Nuxt.js build artifacts | ~4 |
| `contains "/.vite/"` | Vite build artifacts | ~4 |
| `contains "/.output/"` | Nitro/Nuxt output | ~4 |
| `contains "/actuator"` | Spring Boot actuator endpoints | ~9 |
| `contains "/_profiler"` | Symfony profiler/phpinfo | ~23 |
| `contains "/_ignition"` | Laravel Ignition debug | ~4 |
| `contains "/__debug"` | Various debug panels | ~8 |
| `contains "phpmyadmin"` | phpMyAdmin (lowercase) | ~4 |
| `contains "phpMyAdmin"` | phpMyAdmin (proper case) | ~4 |
| `contains "phpinfo"` | phpinfo variants (non-.php) | ~8+ |
| `contains "/debug/default/view"` | Yii framework debug | ~27 |
| `contains ".htaccess"` | Apache config exposure | ~4 |
| `contains ".sql"` | Database dump files | ~20+ |
| `contains "docker-compose"` | Docker Compose config | ~18+ |
| `contains "serverless.yml"` | Serverless framework config | ~8 |
| `contains "private.key"` | Private key exposure | ~5 |
| `contains "backup.zip"` | Backup archive exposure | ~5 |
| `contains "backup.tar.gz"` | Backup archive exposure | ~5 |
| `contains "secrets.yml"` | Secret config files | ~14 |
| `contains "aws-secret"` | AWS secret files | ~10 |
| `contains "aws-credentials"` | AWS credential files | ~4 |
| `contains ".npmrc"` | npm registry credentials | ~5 |
| `contains ".pypirc"` | PyPI registry credentials | ~4 |
| `contains ".boto"` | AWS Boto credentials | ~7 |
| `contains "composer.lock"` | PHP Composer lock file | ~5 |
| `contains "/graphql"` | GraphQL endpoint probes | ~4+ |
| `contains "/graphiql"` | GraphQL IDE probes | ~4 |
| `contains "server-info"` | Apache server-info | ~6 |
| `contains "server-status"` | Apache server-status | ~6 |
| `contains "/administrator/"` | Joomla admin panel | ~14 |
| `contains "/feed/"` | RSS feed probe | ~9 |
| `contains "/package/dynamic_js/"` | Wix/third-party bot scanner | ~60+ |

---

## Not Blocked (Intentionally Excluded)

| Path | Reason |
|---|---|
| `/robots.txt` | Legitimate; already excluded in UA bot rule |
| `/.well-known/*` | May be needed for SSL cert validation (ACME). Malicious .php files within it are caught by the .php rule |
| `/swagger/*` | MedRecPro uses Swashbuckle Swagger for API docs |
| `/admin/*` | MedRecPro may have legitimate admin routes |
| `/user/login` | Could be a legitimate MedRecPro route |
| `/ads.txt` | Minor probe (11 hits), not worth potential false positive |
| `/sitemap.txt` | Could be legitimate |
| `/manifest.json` | Could be legitimate for PWA |
| Various `.js` hash files | Low hit count, would require many specific rules |

---

## Instructions - Creating the Rule in Cloudflare Dashboard

1. Navigate to: **Security > Security Rules**
2. Click: **Create Rule > Custom Rules**
3. Rule name: `Block Bot Probe Paths (URI)`
4. Click: **"Edit expression"** (to paste the raw expression above)
5. Paste the **Expression** block above (single line, starts with `(http.request...`)
6. Under **"Then take action"**: select **Block**
7. Click **Deploy**
