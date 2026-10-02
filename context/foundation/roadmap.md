---
project: "Battle Map Generator dla D&D (DM Toolkit)"
version: 1
status: draft                    # draft | active | locked
created: 2026-09-22
updated: 2026-10-02
prd_version: 2
main_goal: learn
top_blocker: time
milestone_id: dm-downloads-session-map
milestone_seq: 1
milestone_status: open           # open | done
---

# Roadmap: Battle Map Generator dla D&D (DM Toolkit)

> Derived from `context/foundation/prd.md` (v2) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Milestone

**M-1: DM pobiera gotową mapę na sesję** — Status: open

- **Intent:** Etap 1 PRD działa end-to-end: DM zakłada konto i loguje się, ustawia parametry starcia (liczba pokoi, typ starcia), generuje mapę wyrównaną do siatki, w razie potrzeby generuje ją ponownie i pobiera PNG gotowy do wgrania do Roll20.
- **Source materials:** `context/foundation/prd.md` (v2; v1 → v2 2026-09-30 po peer feedback: liczba pokoi i arena bossa, otwarta rejestracja)
- **Done when:** every F-NN and S-NN below is `done`, a przepływ rejestracja/logowanie → generowanie → regeneracja → pobranie przechodzi w Chrome i Firefox na wdrożonej aplikacji.
- **Scope anchors:** US-01; FR-001–FR-006; NFR (czas generacji ≤ 5 min, izolacja danych między kontami, Chrome i Firefox); Guardrail wyrównania do siatki.

## Vision recap

DM przed sesją D&D potrzebuje czytelnej mapy bitewnej wyrównanej do siatki, a generatory obrazów AI dają ładną grafikę bez użytecznej siatki. Produkt odwraca to podejście: DM opisuje zamiar (liczbę pokoi i typ starcia), a system proceduralnie, bez AI, składa grywalny układ pokoi i korytarzy z kafelków. Jedyna rzecz, bez której mapa jest bezużyteczna, to poprawne wyrównanie do siatki.

## North star

**S-01: DM generuje mapę, widzi ją wyrównaną do siatki i pobiera PNG** — pierwsza pełna ścieżka od kliknięcia do pliku gotowego dla Roll20; przy celu „nauka” od razu ćwiczy wszystkie nowe elementy (generator, kontrakt siatki, render i eksport w przeglądarce, testy w Chrome i Firefox).

> „North star” (gwiazda przewodnia) oznacza tu najmniejszy slice end-to-end, którego dowiezienie udowadnia, że produkt działa — dlatego idzie tak wcześnie, jak pozwalają zależności, bo reszta ma sens tylko wtedy, gdy on działa.

## At a glance

| ID   | Change ID                | Outcome (user can …)                                                                 | Prerequisites | PRD refs                      | Status   |
| ---- | ------------------------ | ------------------------------------------------------------------------------------ | ------------- | ----------------------------- | -------- |
| F-01 | account-store-foundation | (foundation) baza kont działa w chmurze, a sesje przetrwają restart i uśpienie aplikacji | —             | FR-001, NFR (dane konta niewidoczne dla innych kont), Access Control | done |
| S-01 | first-map-download       | DM generuje mapę, widzi podgląd wyrównany do siatki i pobiera zgodny z nim PNG        | —             | US-01, FR-003, FR-004, FR-006, NFR (czas generacji, Chrome i Firefox), Guardrail wyrównania do siatki | done        |
| S-02 | regenerate-with-new-seed | DM generuje mapę ponownie i dostaje inny układ przy tych samych parametrach           | S-01          | US-01, FR-005                 | ready    |
| S-03 | encounter-parameters     | DM wybiera liczbę pokoi (2–12) i typ starcia; walka z bossem dodaje arenę skalowaną rozmiarem bossa | S-01 | US-01, FR-002, Business Logic | done |
| S-04 | dm-email-login           | DM zakłada konto i loguje się e-mailem i hasłem; bez zalogowania nie wygeneruje mapy, a generowania na konto są limitowane | F-01, S-01 | US-01, FR-001, NFR (dane konta niewidoczne dla innych kont), Access Control | done |

## Streams

Navigation aid — groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme            | Chain                    | Note                                                                                      |
| ------ | ---------------- | ------------------------ | ----------------------------------------------------------------------------------------- |
| A      | Mapa i generator | `S-01` → `S-02` → `S-03` | Rdzeń produktu i najwięcej nowej technologii do przećwiczenia; S-02 i S-03 są niezależne w grafie, ale oba zmieniają żądanie generowania i formularz — lepiej po kolei niż równolegle. |
| B      | Dostęp           | `F-01` → `S-04`          | Dołącza do A w S-04 (logowanie osłania generowanie z S-01). **S-03 ∥ S-04 to zalecana para do równoległej realizacji** — różne strumienie, prawie bez wspólnych plików. |

## Baseline

What's already in place in the codebase as of `2026-09-22` (auto-researched + user-confirmed), odświeżone 2026-10-02 po F-01, S-01, S-03 i S-04.
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** present — widok główny generuje mapę, renderuje podgląd z kafelków i pobiera PNG (S-01), z kontraktem UI (tokeny, komponenty, stany podglądu); testy renderowania na wspólnych siatkach.
- **Backend / API:** present — generowanie mapy BSP z własnym PRNG, rozmiar mapy zależny od liczby pokoi i areny bossa (S-03, `api/Maps/`), wymaga sesji (401 bez niej) i ograniczone limitem zapytań na konto (S-04); testy jednostkowe algorytmu.
- **Data:** present — baza kont w chmurze z migracją początkową (F-01).
- **Auth:** present — rejestracja i logowanie e-mailem/hasłem, sesja w ciasteczku chronionym kluczami z bazy, generowanie wymaga sesji i jest limitowane na konto (S-04, `api/Auth/`, `api/Program.cs`).
- **Deploy / infra:** present — `.github/workflows/deploy.yml` (build → OIDC → App Service F1 → smoke test `/api/health`), pierwsze wdrożenie 2026-09-22; testy (API, web, e2e w Chrome i Firefox) bramkują merge w `ci.yml`.
- **Observability:** partial — domyślne logowanie + logi kontenera App Service (`az webapp log tail`); brak śledzenia błędów, wystarczające dla MVP.

## Foundations

### F-01: Baza kont i trwałe sesje

- **Outcome:** (foundation) baza kont działa w chmurze w tym samym regionie co aplikacja, połączenie przeżywa wybudzanie bazy, a klucze sesji są trwałe — restart lub uśpienie aplikacji nie wylogowuje użytkownika.
- **Change ID:** account-store-foundation
- **PRD refs:** FR-001, NFR (dane konta niewidoczne dla innych kont), Access Control
- **Unlocks:** S-04; weryfikacja „wymuszony restart na Azure nie unieważnia sesji” (ryzyko H/H z risk register `infrastructure.md`)
- **Prerequisites:** zasób bazy utworzony ręcznie przez właściciela (darmowa oferta, auto-pause at limit — human-only wg `AGENTS.md`)
- **Parallel with:** S-01, S-02, S-03
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Wydzielone, bo łączy ręczne kroki w Azure z dwoma znanymi pułapkami (utrata kluczy sesji, błąd przy wybudzaniu bazy); wpięcie ich dopiero w S-04 zamieniłoby debugowanie logowania w debugowanie infrastruktury. Nie buduje logowania — to robi S-04.
- **Status:** done

## Slices

### S-01: DM generuje mapę, widzi ją i pobiera PNG

- **Outcome:** DM klika „generuj” i widzi mapę z pokojami i korytarzami wyrównaną do siatki (na jednym domyślnym rozmiarze i typie starcia), a potem pobiera PNG w stałej rozdzielczości 140 px na kratkę, zgodny z podglądem, w Chrome i Firefox.
- **Change ID:** first-map-download
- **PRD refs:** US-01, FR-003, FR-004, FR-006, NFR (czas generacji, Chrome i Firefox), Guardrail wyrównania do siatki
- **Prerequisites:** —
- **Parallel with:** F-01
- **Blockers:** —
- **Unknowns:**
  - Jaki domyślny rozmiar i typ mapy przyjąć, zanim S-03 ustali znaczenie S/M/L? — Owner: user. Block: no (jeden stały rozmiar, wymieniony w S-03).
  - Czy przed S-04 generowanie na produkcji ma być publiczne? (zob. Open Roadmap Questions #2) — Owner: user. Block: no.
- **Risk:** Najszerszy slice milestone'u (generator, kontrakt siatki, render, eksport, testy w dwóch przeglądarkach) — wybrany świadomie jako pierwszy, bo PRD ma jeden przepływ (US-01) i dopiero pobranie domyka kryterium sukcesu; jeśli `/10x-plan` uzna go za zbyt szeroki, podzielić na „podgląd” i „pobranie”, a nie na warstwy.
- **Status:** done

### S-02: DM generuje mapę ponownie

- **Outcome:** DM klika „generuj ponownie” i bez błędu dostaje inny układ (nowy seed) przy tych samych parametrach; pobrany plik odpowiada aktualnemu podglądowi.
- **Change ID:** regenerate-with-new-seed
- **PRD refs:** US-01, FR-005
- **Prerequisites:** S-01
- **Parallel with:** F-01, S-03, S-04
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Mały slice, ale sprawdza determinizm: ten sam seed musi dawać tę samą mapę, a nowy — inną; każda regeneracja obciąża limity CPU planu F1.
- **Status:** ready

### S-03: DM ustawia parametry starcia

- **Outcome:** DM wybiera liczbę pokoi (2–12, domyślnie 6) i typ starcia (potyczka / walka z bossem); mapa ma dokładnie tyle pokoi, a przy walce z bossem jeden z nich jest oznaczoną areną o podłodze co najmniej 8×8 / 10×10 / 12×12 dla bossa Dużego / Ogromnego / Gigantycznego.
- **Change ID:** encounter-parameters
- **PRD refs:** US-01, FR-002, Business Logic
- **Prerequisites:** S-01
- **Parallel with:** S-02, S-04
- **Blockers:** —
- **Unknowns:**
  - Czy 12 pokoi z areną dla bossa Gigantycznego zawsze mieści się w obecnym limicie rozmiaru mapy, czy limit trzeba podnieść (koszt CPU na planie F1, rozmiar PNG przy 140 px na kratkę)? — Owner: team (`/10x-plan`). Block: no.
- **Risk:** Zmienia kontrakt siatki (nowe pojęcie areny bossa) i zmienia mapę dla każdego seeda, więc API, klient i wspólne siatki testowe zmieniają się w jednym commicie.
- **Status:** done

### S-04: DM loguje się e-mailem i hasłem

- **Outcome:** DM zakłada konto (otwarta rejestracja, bez weryfikacji e-mail) i loguje się e-mailem i hasłem; niezalogowany użytkownik widzi tylko logowanie / rejestrację i nie może wygenerować mapy; liczba generowań na konto jest ograniczona; pełny przepływ rejestracja/logowanie → generowanie → regeneracja → pobranie działa w Chrome i Firefox na wdrożonej aplikacji.
- **Change ID:** dm-email-login
- **PRD refs:** US-01, FR-001, NFR (dane konta niewidoczne dla innych kont), Access Control
- **Prerequisites:** F-01, S-01
- **Parallel with:** S-02, S-03
- **Blockers:** —
- **Unknowns:**
  - Jaki limit generowań na konto (ile i w jakim oknie czasu), żeby otwarta rejestracja nie wyczerpała limitów CPU planu F1? — Owner: user. Block: no (`/10x-plan` proponuje wartość domyślną).
- **Risk:** Otwarta rejestracja wystawia generowanie każdemu, kto założy konto — limit generowań na konto zastępuje ochronę, którą dziś daje brak kont; `/10x-plan` może wydzielić limit w osobną zmianę, jeśli slice okaże się za szeroki.
- **Status:** done

## Backlog Handoff

| Roadmap ID | Change ID                | Suggested issue title                                        | Ready for `/10x-plan` | Notes |
| ---------- | ------------------------ | ------------------------------------------------------------ | --------------------- | ----- |
| F-01       | account-store-foundation | Baza kont w chmurze i trwałe sesje po restarcie              | yes                   | Wymaga ręcznego utworzenia bazy przez właściciela; można równolegle z S-01 |
| S-01       | first-map-download       | Generowanie mapy z podglądem wyrównanym do siatki i pobraniem PNG | yes              | Run `/10x-plan first-map-download` |
| S-02       | regenerate-with-new-seed | Regeneracja mapy z nowym seedem                              | yes                   | Run `/10x-plan regenerate-with-new-seed`; nie równolegle z S-03 (wspólny formularz i żądanie) |
| S-03       | encounter-parameters     | Parametry starcia: liczba pokoi i arena bossa                 | yes                   | Run `/10x-plan encounter-parameters`; zalecana para równoległa z S-04 |
| S-04       | dm-email-login           | Rejestracja i logowanie e-mail/hasło, ochrona i limit generowania | yes              | Run `/10x-plan dm-email-login`; zalecana para równoległa z S-03 |

## Open Roadmap Questions

1. **Kryterium drugorzędne sukcesu** — „Nie ustalono osobnego kryterium drugorzędnego dla MVP.” Do rozstrzygnięcia: pozostawienie braku dodatkowego kryterium albo jego określenie; biblioteka pozostaje poza MVP. — Owner: autor. Block: nic (nie wpływa na kolejność).
2. ~~**Publiczne generowanie na produkcji przed logowaniem**~~ — Rozwiązane 2026-10-02 (S-04 wdrożone): `/api/maps/generate` wymaga sesji (401 bez niej) i jest limitowane na konto, więc generowanie nie jest już publiczne na produkcji.

(PRD Open Questions #1 — konflikt wymagań kompatybilności z filtrem PRD — rozwiązane 2026-09-16; #3 tworzenie kont i #4 parametry starcia — rozwiązane 2026-09-30 w PRD v2 (otwarta rejestracja; liczba pokoi i arena bossa), pominięte.)

## Parked

- **Generowanie obrazów przez AI jako silnik map** — Why parked: PRD §Non-Goals (te same problemy z siatką co istniejące generatory).
- **Działanie offline** — Why parked: PRD §Non-Goals.
- **Hierarchia Kampania → Sesja → Mapa z CRUD** — Why parked: PRD §Non-Goals (etap 2).
- **Zapis mapy w bibliotece z tytułem** — Why parked: PRD §Non-Goals (etap 2, poza kryteriami sukcesu MVP).
- **Integracja z API Roll20** — Why parked: PRD §Non-Goals (eksport pozostaje ręcznym pobraniem PNG).
- **Dodatkowe motywy graficzne, pułapki, skarby, dekoracje** — Why parked: PRD §Non-Goals (MVP ma jeden zestaw kafelków).
- **Druk na wielu stronach w skali 1 cal na kratkę** — Why parked: PRD §Non-Goals (PNG 140 px na kratkę wystarcza do samodzielnego wydruku).
- **Rozbudowa parametrów generowania poza liczbę pokoi i typ** — Why parked: PRD FR-002 (rozbudowa to praca po MVP).
- **Kandydat na M-2: pętle w układzie (FR-007)** — proponowany Change ID `looping-dungeons`: DM generuje „okrężny” loch, który drużyna obchodzi w dowolną stronę bez cofania się. Why parked: nice-to-have, poza zakresem M-1; zmienia mapę dla każdego seeda.
- **Kandydat na M-2: teren trudny i nieprzechodni (FR-008)** — proponowany Change ID `difficult-terrain`: mapa z terenem kosztującym podwójny ruch i przeszkodami nie do przejścia, bez utraty spójności. Why parked: nice-to-have, poza zakresem M-1; nowe pojęcia w siatce semantycznej.
- **Wiele poziomów lochu połączonych schodami** — Why parked: PRD §Non-Goals (peer feedback 2026-09-30; duża zmiana zakresu).
- **Prosty wirtualny stół** — Why parked: PRD §Non-Goals (sprzeczny z eksportem PNG do Roll20; osobny produkt).
- **Subskrypcje / płatne plany z większym limitem generowań** — Why parked: PRD §Non-Goals (gdy urośnie liczba użytkowników; wyższy plan hostingu to decyzja billingowa właściciela).
- **Weryfikacja adresu e-mail przy rejestracji** — Why parked: PRD §Non-Goals (wymagałaby usługi wysyłki poczty).
- **Większa różnorodność układów (WFC lub dalsze reguły różnicujące walki z bossem)** — Why parked: PRD FR-003 (czyste BSP w MVP); minimum dla walki z bossem (arena) należy do S-03.
- **Wydzielenie generatora do osobnego serwisu** — Why parked: `shape-notes.md` §Forward: post-MVP.
- **Kroki AI w CI (automatyczny code review, aktualizacja dokumentacji)** — Why parked: `shape-notes.md` §Forward: post-MVP; przy głównym ryzyku „czas” nie wchodzi do milestone'u.

## Milestone History

## Done

- **F-01: (foundation) baza kont działa w chmurze w tym samym regionie co aplikacja, połączenie przeżywa wybudzanie bazy, a klucze sesji są trwałe — restart lub uśpienie aplikacji nie wylogowuje użytkownika.** — Archived 2026-09-25 → `context/archive/2026-09-23-account-store-foundation/`. Lesson: —.
- **S-01: DM klika „generuj” i widzi mapę z pokojami i korytarzami wyrównaną do siatki (na jednym domyślnym rozmiarze i typie starcia), a potem pobiera PNG w stałej rozdzielczości 140 px na kratkę, zgodny z podglądem, w Chrome i Firefox.** — Archived 2026-09-29 → `context/archive/2026-09-25-first-map-download/`. Lesson: —.
- **S-03: DM wybiera liczbę pokoi (2–12, domyślnie 6) i typ starcia (potyczka / walka z bossem); mapa ma dokładnie tyle pokoi, a przy walce z bossem jeden z nich jest oznaczoną areną o podłodze co najmniej 8×8 / 10×10 / 12×12 dla bossa Dużego / Ogromnego / Gigantycznego.** — Archived 2026-10-02 → `context/archive/2026-09-30-encounter-parameters/`. Lesson: —.
