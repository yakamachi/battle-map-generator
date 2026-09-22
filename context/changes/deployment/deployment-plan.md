---
change: first-deployment
status: in-progress
created: 2026-09-22
platform: Azure App Service (Linux, F1)
---

# Pierwsze wdrożenie: szkielet na Azure App Service (F1) przez GitHub Actions + OIDC

## Kontekst

`context/foundation/infrastructure.md` jest kompletny: platforma to Azure App Service Linux F1 (B1 jako wyjście awaryjne), runner-up Fly.io, jest risk register i operational story. Decyzje infrastrukturalne mamy więc zamknięte. Brakuje wykonania:
- nie ma zasobów w Azure, remote na GitHubie, workflow `.github/`, a lokalnie brakuje `az` i `gh`;
- `api/Program.cs` to wciąż szablon weatherforecast i nie serwuje plików statycznych;
- restrukturyzacja monorepo (przeniesienia w `context/`, nowe `AGENTS.md`/`CLAUDE.md`) nie jest scommitowana.

Cel: „cienki” deploy. `web/` budujemy do `build/client`, kopiujemy do `api/wwwroot`, robimy `dotnet publish` i wypychamy na Azure z GitHub Actions. Uwierzytelnienie przez OIDC (federated credential na user-assigned managed identity), więc **nie ma żadnego hasła ani client secretu, który wygasa**. W GitHubie są tylko trzy identyfikatory (client/tenant/subscription ID) jako *variables*, nie secrets. Bez bazy: Azure SQL, Data Protection w DB i `EnableRetryOnFailure` dochodzą razem z logowaniem.

O Deployment Center: jest darmowy, ale to tylko kreator w portalu. Generuje workflow GitHub Actions i sam tworzy tożsamość. Piszemy workflow sami, bo potrzebny jest build monorepo (node + dotnet), którego kreator nie zrobi dobrze.

Plan po zatwierdzeniu zapisuję w `context/changes/deployment/deployment-plan.md` (decyzja użytkownika).

## Co przygotowujesz sam (checklista właściciela)

**Teraz, do pierwszego wdrożenia:**
1. ✅ **Konto Azure (Free account, 200 USD kredytu, karta podpięta)**: gotowe. Uwagi:
   - Research zapisał ryzyko „Free Trial ma kwotę F1 = 0”. Sprawdzamy to w Fazie 2 przy `az appservice plan create --sku F1`.
     - Jeśli F1 przejdzie, idziemy dalej.
     - Jeśli dostaniemy błąd kwoty, masz dwie opcje. (a) B1 (ok. 13 USD/mies.), opłacany z kredytu. (b) Upgrade do Pay-As-You-Go: portal → Subscriptions → Upgrade. Darmowe usługi i niewykorzystany kredyt zostają. Obie decyzje podejmujesz sam.
   - **Kredyt wygasa po 30 dniach.** Bez upgrade'u do Pay-As-You-Go subskrypcja zostanie wtedy wyłączona, a aplikacja przestanie działać. Przed końcem okresu (sprawdź datę w Cost Management) zrób upgrade ręcznie. Sam F1 i SQL free offer dalej kosztują 0 zł.
2. **Budget alert** na subskrypcji (Cost Management → Budgets, np. 5 USD, mail do Ciebie). Przy kredycie też warto, bo pokaże, że coś zaczyna go zjadać.
3. **Konto GitHub** (masz) i zgoda na utworzenie **prywatnego** repo `battle-map-generator`. GitHub Actions dla prywatnego repo na Free ma 2000 min/mies., a jeden deploy to ok. 3–5 min.
4. **Instalacja i logowanie CLI** u siebie: `sudo pacman -S azure-cli github-cli`, `az login`, `gh auth login`. Logowanie jest interaktywne, więc robisz je sam (`! az login` w sesji).
5. **Wybór regionu i nazwy aplikacji.** Nazwa `battlemap-xxx` będzie adresem `https://battlemap-xxx.azurewebsites.net`, musi być globalnie unikalna.
6. **Zatwierdzenie poleceń tworzących zasoby** (Faza 2) i przypisania roli. Agent je przygotuje, a Ty uruchamiasz albo akceptujesz.

Nic nie wygasa: OIDC + managed identity nie mają hasła do rotacji. Jedyne „dane” w GitHubie to 4 jawne identyfikatory.

**Później (kolejne wdrożenia, wg infrastructure.md), nie teraz:**
- **Azure SQL Database free offer** (przy logowaniu): tworzysz w tym samym regionie, z opcją **„auto-pause at limit”**. Nigdy „continue with charges” bez decyzji. Hasło admina SQL ustawiasz sam. Docelowo lepiej Entra auth z managed identity aplikacji, wtedy znowu bez hasła.
- **Ewentualny upgrade F1 → B1** (ok. 13 USD/mies.), jeśli uderzymy w limit CPU (60 min/dzień) lub transferu, albo przy własnej domenie/TLS.
- **Własna domena** (opcjonalnie, wymaga B1): zakup u rejestratora i rekordy DNS robisz sam.
- **Operacje destrukcyjne**: usuwanie bazy lub zasobów, rotacja haseł, zmiana tieru. Zawsze ręcznie w portalu.

## Faza 0: przygotowanie repo i narzędzi
- [ ] **(człowiek, zatwierdzenie)** Commit obecnej restrukturyzacji monorepo jako osobny commit, przed zmianami wdrożeniowymi.
- [ ] **(człowiek)** Instalacja CLI (CachyOS): `sudo pacman -S azure-cli github-cli`, potem `az login`, `gh auth login`.
- [ ] Sprawdzenie: `az account show` (subskrypcja Free Trial, zapisać datę wygaśnięcia kredytu w planie), `az webapp list-runtimes --os linux | grep -i dotnet` (oczekiwane `DOTNETCORE:10.0`), `az appservice list-locations --sku F1 --linux-workers-enabled` (wybór regionu, np. `polandcentral` albo `westeurope`).

## Faza 1: zmiany w kodzie (agent)
- [x] `api/Program.cs`:
  - usunąć weatherforecast;
  - dodać `app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }))`;
  - dodać `UseDefaultFiles()` + `UseStaticFiles(...)`. `OnPrepareResponse` ustawia `Cache-Control: no-cache` dla `index.html` i długi cache dla `/assets/*` (hashowane);
  - dodać `app.MapFallback("/api/{**rest}", () => Results.NotFound())`, żeby nieznane `/api/*` nie zwracały SPA;
  - dodać `app.MapFallbackToFile("index.html")` na końcu;
  - zostawić `UseHttpsRedirection` (App Service na Linuksie ustawia forwarded headers).
- [x] `global.json` w katalogu głównym: przypięcie SDK `10.0.100` z `rollForward: latestFeature` (ryzyko z registera „.NET patch drift”).
- [x] `.gitignore`: dodać `api/wwwroot/` (artefakt buildu, nie źródło), `api/bin/`, `api/obj/` i `web/build/`, jeśli jeszcze ich nie ma.
- [x] `web/vite.config.ts`: `server.proxy` dla `/api` → `http://localhost:5108` (dev lokalny, krok 6 z infrastructure.md).
- [x] `.github/workflows/deploy.yml`:
  - `on: push: branches [main]` + `workflow_dispatch` (ręczny re-run służy też do rollbacku);
  - `permissions: { id-token: write, contents: read }`, `concurrency: deploy-production`;
  - job **build**: `actions/setup-node` (Node 24 LTS, cache npm z `web/package-lock.json`), `npm ci`, `npm run typecheck`, `npm run build` w `web/`; `actions/setup-dotnet` (z `global.json`); `cp -r web/build/client/. api/wwwroot/`; `dotnet publish api -c Release -o publish`; `upload-artifact`;
  - job **deploy** (`environment: production`): `download-artifact`, `azure/login@v2` z `client-id/tenant-id/subscription-id: ${{ vars.AZURE_* }}`, `azure/webapps-deploy@v3` z `app-name: ${{ vars.AZURE_WEBAPP_NAME }}`, `package: publish`;
  - smoke test: `curl --retry 10 --retry-delay 15 --fail https://<app>.azurewebsites.net/api/health` (F1 ma zimny start).

## Faza 2: zasoby Azure (człowiek uruchamia albo zatwierdza każde polecenie)
Zmienne: `RG=rg-battlemap`, `LOC=<region>`, `APP=battlemap-<unikalny-sufiks>`, `ID=id-battlemap-deploy`.
- [ ] `az group create -n $RG -l $LOC`
- [ ] `az appservice plan create -g $RG -n plan-battlemap --is-linux --sku F1`
- [ ] `az webapp create -g $RG -p plan-battlemap -n $APP --runtime "DOTNETCORE:10.0"` (separator z Fazy 0)
- [ ] `az webapp update -g $RG -n $APP --https-only true`; `az webapp config set -g $RG -n $APP --startup-file "dotnet battle-map-generator-api.dll"`
- [ ] `az webapp log config -g $RG -n $APP --docker-container-logging filesystem`
- [ ] **Tożsamość deployu bez sekretów:**
  - `az identity create -g $RG -n $ID`
  - `az identity federated-credential create -g $RG --identity-name $ID -n github-production --issuer https://token.actions.githubusercontent.com --subject repo:<owner>/battle-map-generator:environment:production --audiences api://AzureADTokenExchange`
  - `az role assignment create --assignee <principalId> --role "Website Contributor" --scope <webapp resource id>` (zakres tylko ta aplikacja: bez DNS, bez billingu, bez innych zasobów)
- [ ] **(człowiek, portal)** Budget alert na subskrypcji (np. 5 USD), bo billing jest human-only.

## Faza 3: GitHub (człowiek zatwierdza, bo to publikacja na zewnątrz)
- [ ] `gh repo create battle-map-generator --private --source . --remote origin` (bez pushu na razie)
- [ ] `gh api -X PUT repos/<owner>/battle-map-generator/environments/production`
- [ ] `gh variable set AZURE_CLIENT_ID|AZURE_TENANT_ID|AZURE_SUBSCRIPTION_ID|AZURE_WEBAPP_NAME` (variables, bo to nie sekrety)
- [ ] Commit zmian z Fazy 1 i `git push -u origin main`, co uruchamia pierwszy deploy.

## Przypadki brzegowe i wsparcie
- **`AADSTS70021` / „no matching federated identity”**: subject musi się zgadzać co do znaku (`repo:owner/name:environment:production`, wielkość liter owner/repo). Sprawdzić `az identity federated-credential list`.
- **403 przy deployu**: propagacja przypisania roli trwa do ok. 5 min, potem re-run joba.
- **Kwota F1 = 0 / „SKU not available” (możliwe na Free Trial)**: najpierw spróbować innego regionu. Potem wybór człowieka: B1 z kredytu albo upgrade do Pay-As-You-Go.
- **Aplikacja nie startuje (`:( Application Error`)**: `az webapp log tail -g $RG -n $APP`. Sprawdzić nazwę dll w startup command i to, że runtime to 10.0.
- **Zimny start F1 (~10–30 s)**: jest wliczony w retry smoke testu.
- **Rollback**: `workflow_dispatch` na poprzednim commicie albo `az webapp deploy --src-path <stary.zip> --type zip`.

## Weryfikacja
1. Lokalnie: `npm run build` w `web/` → `cp -r web/build/client/. api/wwwroot/` → `dotnet run --project api`:
   - `/` zwraca SPA;
   - `/cokolwiek` zwraca `index.html`, a routing działa;
   - `/api/health` zwraca 200 JSON;
   - `/api/nieistnieje` zwraca 404;
   - `curl -I` pokazuje `no-cache` na `index.html` i długi cache na `/assets/*`.
2. `dotnet build api` i `npm run typecheck` w `web/` przechodzą.
3. CI: oba joby zielone, smoke test przechodzi.
4. Produkcja: `https://$APP.azurewebsites.net/` ładuje SPA, `/api/health` zwraca 200, `az webapp log tail` bez błędów.
5. Po sukcesie: zapis planu (z odhaczonymi krokami) do `context/changes/deployment/deployment-plan.md`.
