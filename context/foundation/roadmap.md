---
project: "Battle Map Generator dla D&D (DM Toolkit)"
version: 1
status: draft                    # draft | active | locked
created: 2026-09-22
updated: 2026-09-22
prd_version: 1
main_goal: learn
top_blocker: time
milestone_id: dm-downloads-session-map
milestone_seq: 1
milestone_status: open           # open | done
---

# Roadmap: Battle Map Generator dla D&D (DM Toolkit)

> Derived from `context/foundation/prd.md` (v1) + auto-researched codebase baseline.
> Edit-in-place; archive when superseded.
> Slices below are listed in dependency order. The "At a glance" table is the index.

## Milestone

**M-1: DM pobiera gotową mapę na sesję** — Status: open

- **Intent:** Etap 1 PRD działa end-to-end: zalogowany DM ustawia parametry starcia, generuje mapę wyrównaną do siatki, w razie potrzeby generuje ją ponownie i pobiera PNG gotowy do wgrania do Roll20.
- **Source materials:** `context/foundation/prd.md` (v1)
- **Done when:** every F-NN and S-NN below is `done`, a przepływ logowanie → generowanie → regeneracja → pobranie przechodzi w Chrome i Firefox na wdrożonej aplikacji.
- **Scope anchors:** US-01; FR-001–FR-006; NFR (czas generacji ≤ 5 min, izolacja danych między kontami, Chrome i Firefox); Guardrail wyrównania do siatki.

## Vision recap

DM przed sesją D&D potrzebuje czytelnej mapy bitewnej wyrównanej do siatki, a generatory obrazów AI dają ładną grafikę bez użytecznej siatki. Produkt odwraca to podejście: DM opisuje zamiar (rozmiar i typ starcia), a system proceduralnie, bez AI, składa grywalny układ pokoi i korytarzy z kafelków. Jedyna rzecz, bez której mapa jest bezużyteczna, to poprawne wyrównanie do siatki.

## North star

**S-01: DM generuje mapę, widzi ją wyrównaną do siatki i pobiera PNG** — pierwsza pełna ścieżka od kliknięcia do pliku gotowego dla Roll20; przy celu „nauka” od razu ćwiczy wszystkie nowe elementy (generator, kontrakt siatki, render i eksport w przeglądarce, testy w Chrome i Firefox).

> „North star” (gwiazda przewodnia) oznacza tu najmniejszy slice end-to-end, którego dowiezienie udowadnia, że produkt działa — dlatego idzie tak wcześnie, jak pozwalają zależności, bo reszta ma sens tylko wtedy, gdy on działa.

## At a glance

| ID   | Change ID                | Outcome (user can …)                                                                 | Prerequisites | PRD refs                      | Status   |
| ---- | ------------------------ | ------------------------------------------------------------------------------------ | ------------- | ----------------------------- | -------- |
| F-01 | account-store-foundation | (foundation) baza kont działa w chmurze, a sesje przetrwają restart i uśpienie aplikacji | —             | FR-001, NFR (dane konta niewidoczne dla innych kont), Access Control | ready    |
| S-01 | first-map-download       | DM generuje mapę, widzi podgląd wyrównany do siatki i pobiera zgodny z nim PNG        | —             | US-01, FR-003, FR-004, FR-006, NFR (czas generacji, Chrome i Firefox), Guardrail wyrównania do siatki | ready    |
| S-02 | regenerate-with-new-seed | DM generuje mapę ponownie i dostaje inny układ przy tych samych parametrach           | S-01          | US-01, FR-005                 | proposed |
| S-03 | encounter-parameters     | DM wybiera rozmiar (S/M/L) i typ starcia, a mapa odpowiada wyborowi                   | S-01          | US-01, FR-002                 | blocked  |
| S-04 | dm-email-login           | DM loguje się e-mailem i hasłem; bez zalogowania nie wygeneruje mapy                  | F-01, S-01    | US-01, FR-001, NFR (dane konta niewidoczne dla innych kont), Access Control | blocked  |

## Streams

Navigation aid — groups items that share a Prerequisites chain. Canonical ordering still lives in the dependency graph below; this table is the proposed reading order across parallel tracks.

| Stream | Theme            | Chain                    | Note                                                                                      |
| ------ | ---------------- | ------------------------ | ----------------------------------------------------------------------------------------- |
| A      | Mapa i generator | `S-01` → `S-02` → `S-03` | Rdzeń produktu i najwięcej nowej technologii do przećwiczenia; S-02 i S-03 mogą iść równolegle. |
| B      | Dostęp           | `F-01` → `S-04`          | F-01 startuje równolegle z S-01; strumień dołącza do A w S-04 (logowanie osłania generowanie z S-01). |

## Baseline

What's already in place in the codebase as of `2026-09-22` (auto-researched + user-confirmed).
Foundations below assume these are present and do NOT re-scaffold them.

- **Frontend:** partial — pusty szkielet React Router 8 SPA (`web/app/routes/home.tsx` to ekran powitalny); brak jakiejkolwiek funkcji produktu, renderowania mapy i testów.
- **Backend / API:** partial — pusty szkielet ASP.NET Core minimal API, tylko `GET /api/health` (`api/Program.cs:38`) i serwowanie SPA; brak generatora, PRNG i projektu testowego.
- **Data:** absent — brak bazy i warstwy dostępu do danych; baza kont w Azure jeszcze nie utworzona.
- **Auth:** absent — brak logowania i ochrony endpointów.
- **Deploy / infra:** present — `.github/workflows/deploy.yml` (build → OIDC → App Service F1 → smoke test `/api/health`), pierwsze wdrożenie 2026-09-22; w CI nie ma jeszcze kroku testów.
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
- **Status:** ready

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
  - Czy przed S-04 generowanie na produkcji ma być publiczne? (zob. Open Roadmap Questions #4) — Owner: user. Block: no.
- **Risk:** Najszerszy slice milestone'u (generator, kontrakt siatki, render, eksport, testy w dwóch przeglądarkach) — wybrany świadomie jako pierwszy, bo PRD ma jeden przepływ (US-01) i dopiero pobranie domyka kryterium sukcesu; jeśli `/10x-plan` uzna go za zbyt szeroki, podzielić na „podgląd” i „pobranie”, a nie na warstwy.
- **Status:** ready

### S-02: DM generuje mapę ponownie

- **Outcome:** DM klika „generuj ponownie” i bez błędu dostaje inny układ (nowy seed) przy tych samych parametrach; pobrany plik odpowiada aktualnemu podglądowi.
- **Change ID:** regenerate-with-new-seed
- **PRD refs:** US-01, FR-005
- **Prerequisites:** S-01
- **Parallel with:** F-01, S-03, S-04
- **Blockers:** —
- **Unknowns:** —
- **Risk:** Mały slice, ale sprawdza determinizm: ten sam seed musi dawać tę samą mapę, a nowy — inną; każda regeneracja obciąża limity CPU planu F1.
- **Status:** proposed

### S-03: DM ustawia parametry starcia

- **Outcome:** DM wybiera rozmiar mapy (S/M/L) i typ starcia (potyczka / walka z bossem), a wygenerowana mapa odpowiada wyborowi.
- **Change ID:** encounter-parameters
- **PRD refs:** US-01, FR-002
- **Prerequisites:** S-01
- **Parallel with:** F-01, S-02, S-04
- **Blockers:** —
- **Unknowns:**
  - Co oznaczają rozmiary S/M/L i jak typ starcia zmienia mapę w MVP? (PRD Open Questions #4) — Owner: user. Block: yes.
- **Risk:** Zmienia kontrakt siatki, jeśli walka z bossem wprowadzi nowe pojęcie (np. arena bossa); największy rozmiar musi zmieścić się w limicie rozmiaru obrazu przeglądarki przy 140 px na kratkę.
- **Status:** blocked

### S-04: DM loguje się e-mailem i hasłem

- **Outcome:** DM loguje się e-mailem i hasłem; niezalogowany użytkownik nie może wygenerować mapy; pełny przepływ logowanie → generowanie → regeneracja → pobranie działa w Chrome i Firefox na wdrożonej aplikacji.
- **Change ID:** dm-email-login
- **PRD refs:** US-01, FR-001, NFR (dane konta niewidoczne dla innych kont), Access Control
- **Prerequisites:** F-01, S-01
- **Parallel with:** S-02, S-03
- **Blockers:** —
- **Unknowns:**
  - Jak powstają konta (2 znane konta na start) i co widzi użytkownik bez logowania? (PRD Open Questions #3) — Owner: user. Block: yes.
- **Risk:** Ostatni, bo osłania istniejące już generowanie i domyka test całego przepływu z kryterium sukcesu; pierwsze wdrożenie z logowaniem sprawdza na żywo trwałość sesji z F-01.
- **Status:** blocked

## Backlog Handoff

| Roadmap ID | Change ID                | Suggested issue title                                        | Ready for `/10x-plan` | Notes |
| ---------- | ------------------------ | ------------------------------------------------------------ | --------------------- | ----- |
| F-01       | account-store-foundation | Baza kont w chmurze i trwałe sesje po restarcie              | yes                   | Wymaga ręcznego utworzenia bazy przez właściciela; można równolegle z S-01 |
| S-01       | first-map-download       | Generowanie mapy z podglądem wyrównanym do siatki i pobraniem PNG | yes              | Run `/10x-plan first-map-download` |
| S-02       | regenerate-with-new-seed | Regeneracja mapy z nowym seedem                              | no                    | Czeka na S-01 |
| S-03       | encounter-parameters     | Parametry starcia: rozmiar S/M/L i typ                        | no                    | Czeka na S-01 i rozstrzygnięcie PRD Open Questions #4 |
| S-04       | dm-email-login           | Logowanie e-mail/hasło i ochrona generowania                 | no                    | Czeka na F-01, S-01 i rozstrzygnięcie PRD Open Questions #3 |

## Open Roadmap Questions

1. **Kryterium drugorzędne sukcesu** — „Nie ustalono osobnego kryterium drugorzędnego dla MVP.” Do rozstrzygnięcia: pozostawienie braku dodatkowego kryterium albo jego określenie; biblioteka pozostaje poza MVP. — Owner: autor. Block: nic (nie wpływa na kolejność).
2. **Tworzenie kont i dostęp bez logowania** — Notatki określają login i płaskie role, ale nie sposób tworzenia kont ani zachowanie przy próbie dostępu bez zalogowania. — Owner: autor. Block: S-04.
3. **Wpływ parametrów rozmiaru i typu starcia na wynik** — Nie określono, jakie rozmiary oznaczają S/M/L ani jak typ starcia zmienia mapę w MVP; historia FR-003 odkłada reguły różnicujące walki z bossem, a FR-002 zachowuje typ starcia jako wejście. — Owner: autor. Block: S-03.
4. **Publiczne generowanie na produkcji przed logowaniem** — Każde wdrożenie S-01–S-03 przed S-04 wystawia generowanie bez logowania na planie F1, którego limity CPU i transferu zatrzymują całą aplikację do północy UTC. Czy wystarczy limit zapytań i limit rozmiaru mapy (risk register `infrastructure.md`), czy generowanie ma być do czasu S-04 niedostępne na produkcji? — Owner: autor. Block: nic (domyślnie: limit zapytań w S-01).

(PRD Open Questions #1 — konflikt wymagań kompatybilności z filtrem PRD — rozwiązane 2026-09-16, pominięte.)

## Parked

- **Generowanie obrazów przez AI jako silnik map** — Why parked: PRD §Non-Goals (te same problemy z siatką co istniejące generatory).
- **Działanie offline** — Why parked: PRD §Non-Goals.
- **Hierarchia Kampania → Sesja → Mapa z CRUD** — Why parked: PRD §Non-Goals (etap 2).
- **Zapis mapy w bibliotece z tytułem** — Why parked: PRD §Non-Goals (etap 2, poza kryteriami sukcesu MVP).
- **Integracja z API Roll20** — Why parked: PRD §Non-Goals (eksport pozostaje ręcznym pobraniem PNG).
- **Dodatkowe motywy graficzne, pułapki, skarby, dekoracje** — Why parked: PRD §Non-Goals (MVP ma jeden zestaw kafelków).
- **Druk na wielu stronach w skali 1 cal na kratkę** — Why parked: PRD §Non-Goals (PNG 140 px na kratkę wystarcza do samodzielnego wydruku).
- **Rozbudowa parametrów generowania poza rozmiar i typ** — Why parked: PRD FR-002 (rozbudowa to praca po MVP).
- **Większa różnorodność układów (WFC lub reguły różnicujące walki z bossem)** — Why parked: PRD FR-003 (czyste BSP w MVP); zakres minimum dla typu starcia rozstrzyga Open Roadmap Questions #3.
- **Wydzielenie generatora do osobnego serwisu** — Why parked: `shape-notes.md` §Forward: post-MVP.
- **Kroki AI w CI (automatyczny code review, aktualizacja dokumentacji)** — Why parked: `shape-notes.md` §Forward: post-MVP; przy głównym ryzyku „czas” nie wchodzi do milestone'u.

## Milestone History

## Done
