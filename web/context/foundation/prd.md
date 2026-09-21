---
project: "Battle Map Generator dla D&D (DM Toolkit)"
version: 1
status: draft
created: 2026-09-16
context_type: greenfield
product_type: web-app
target_scale:
  users: small
  qps: low
  data_volume: small
timeline_budget:
  mvp_weeks: 5
  hard_deadline: 2027-01-10
  after_hours_only: true
---

## Vision & Problem Statement

Mistrzowie Gry (DM) prowadzący sesje D&D potrzebują przed każdą sesją czytelnej,
wyrównanej do siatki mapy bitewnej dopasowanej do konkretnego starcia. Dzisiejsze
opcje zawodzą: generatory AI obrazów (np. ChatGPT) produkują grafikę, która nie
działa jako mapa — brak siatki, trzeba ją nakładać ręcznie, albo elementy w ogóle
nie pasują do zamierzonego starcia i są nieczytelne. Ręczne składanie map z
gotowych zasobów zajmuje czas, którego DM nie ma w wieczornym przygotowaniu do
sesji.

Insight: użytkownik nie chce projektować mapy "od zera" na płótnie z pełną
swobodą — chce opisać zamierzenie (rozmiar starcia, typ starcia: zwykła
potyczka vs walka z bossem) i dostać gotową, użyteczną mapę wygenerowaną z
ograniczonego, dobrze zaprojektowanego zestawu opcji. To odróżnia potrzebę od
istniejących narzędzi: "canvas + własne elementy" (za dużo swobody, za mało
gotowości) i generatory obrazów AI (ładny obrazek, bezużyteczny jako mapa do gry).
Zakres opcji generatora może rosnąć z czasem (styl graficzny, przeszkody,
mechaniki specjalne dla starć z bossem), ale rdzeń to: opisz zamiar → dostań
gotową, grywalną mapę.

## User & Persona

**Primary:** Mistrz Gry (DM) prowadzący własne sesje D&D — na start: autor
projektu, korzystający z narzędzia bezpośrednio przed sesją, żeby przygotować
mapę bitewną pod konkretne starcie i wyeksportować ją do Roll20.

### Secondary persona

Inni DM-owie (znajomi autora) — potencjalni kolejni użytkownicy tego samego
narzędzia, jeśli pomysł się sprawdzi. Nie są celem MVP, ale kształt access
control i skali powinien nie wykluczać rozszerzenia w przyszłości.

## Success Criteria

### Primary

- Etap 1 działa end-to-end: DM loguje się, ustawia parametry starcia (rozmiar,
  typ: zwykła potyczka / walka z bossem), generuje mapę, widzi
  wynikowy PNG i pobiera go — bez błędu, gotowy do wgrania do Roll20.

### Secondary

- Nie ustalono osobnego kryterium drugorzędnego dla MVP. Zapis mapy w bibliotece
  z tytułem należy wyłącznie do etapu 2, nie jest kryterium sukcesu MVP.

# TODO: Secondary success criterion — see Open Questions

### Guardrails

- Wygenerowana mapa jest zawsze poprawnie wyrównana do siatki (kafle nie są
  przesunięte, ucięte ani niespójne) — to jedyna rzecz, bez której mapa jest
  bezużyteczna, nawet jeśli reszta przepływu działa.

## User Stories

### US-01: DM generuje i pobiera mapę bitewną na sesję

- **Given** zalogowany DM
- **When** ustawia parametry starcia (rozmiar, typ) i klika "generuj"
- **Then** widzi wygenerowaną mapę jako PNG, wyrównaną do siatki; może ją
  wygenerować ponownie (inny seed) jeśli nie jest zadowolony, a na koniec
  pobrać plik gotowy do wgrania do Roll20

#### Acceptance Criteria
- Wygenerowana mapa jest zawsze poprawnie wyrównana do siatki (kafle nie są
  przesunięte/ucięte)
- Regeneracja z tymi samymi parametrami daje inny układ (inny seed), bez
  błędu
- Pobrany plik PNG otwiera się poprawnie i odpowiada podglądowi

## Functional Requirements

### Access

- FR-001: DM może zalogować się (e-mail/hasło). Priority: must-have
  > Socratic: Kontrargument rozważony: "przy 2 znanych kontach login to zbędna
  > złożoność, prościej byłoby bez auth". Rozstrzygnięcie: zostaje login —
  > przyda się przy ewentualnym rozszerzeniu do innych DM-ów, i to też element
  > workflow, który autor chce przećwiczyć.

### Generowanie mapy

- FR-002: DM może ustawić parametry generowania mapy (rozmiar starcia, typ starcia: potyczka / walka z bossem). Priority: must-have
  > Socratic: Kontrargument rozważony: "za mało parametrów — generator będzie
  > sztywny/mało użyteczny". Rozstrzygnięcie: zostaje minimalny zestaw w MVP,
  > rozbudowa parametrów to praca po MVP.
- FR-003: DM może wygenerować spójną mapę bitewną z pokojami i korytarzami, wyrównaną do siatki. Priority: must-have
  > Socratic: Kontrargument rozważony: "BSP może dawać zbyt monotonne układy
  > dla walki z bossem". Rozstrzygnięcie: czyste BSP zostaje w MVP; większa
  > różnorodność (WFC lub reguły różnicujące bossfighty) to praca po MVP.
- FR-004: DM może zobaczyć podgląd wygenerowanej mapy jako PNG. Priority: must-have
  > Socratic: Kontrargument rozważony: "podgląd bez edycji może być
  > niewystarczający przy drobnym błędzie generacji". Rozstrzygnięcie:
  > rozszerzone o FR-005 (regeneracja) jako wentyl bezpieczeństwa zamiast
  > pełnej edycji.
- FR-005: DM może wygenerować mapę ponownie (nowy seed, te same parametry), jeśli wynik go nie satysfakcjonuje. Priority: must-have

### Eksport

- FR-006: DM może pobrać wygenerowany PNG. Priority: must-have
  > Socratic: Brak kontrargumentu — FR stoi bez zmian.

## Non-Functional Requirements

- Generacja mapy kończy się i pokazuje wynik użytkownikowi w rozsądnym,
  ograniczonym czasie — górna granica akceptowalna to 5 minut, celem jest
  wyraźnie szybciej.
- Dane mapy/kampanii danego konta nie są widoczne dla innych kont.
- Produkt działa poprawnie w najnowszych wersjach Chrome i Firefox.

## Business Logic

System proceduralnie generuje spójny, wyrównany do siatki układ mapy bitewnej (pokoje + korytarze) na podstawie zadanych parametrów starcia i rozmiaru — deterministycznie, bez AI.

Reguła konsumuje jako wejście: rozmiar mapy (S/M/L) i typ starcia (zwykła
potyczka / walka z bossem), podane przez DM w formularzu. Wyjściem jest gotowy,
grywalny układ mapy — pokoje i korytarze rozmieszczone spójnie i wyrównane do
siatki, wyrenderowane graficznie z zestawu kafelków. DM spotyka tę regułę w
momencie kliknięcia "generuj": zamiast projektować mapę ręcznie, opisuje
zamiar, a system zwraca gotowy wynik do ewentualnej regeneracji lub pobrania.

# TODO: Wpływ parametrów rozmiaru i typu starcia na wynik — see Open Questions

## Access Control

Login (e-mail/hasło). Płaski model ról — każde konto ma te same uprawnienia,
nie ma podziału na właściciela kampanii vs gościa. Start: 2 konta (autor +
narzeczona). Model nie wyklucza dodania kolejnych kont w przyszłości (nisza
hobbystyczna), ale role pozostają płaskie, dopóki nie pojawi się konkretna
potrzeba rozróżnienia uprawnień.

# TODO: Tworzenie kont i dostęp bez logowania — see Open Questions

## Non-Goals

- **Prawdziwe AI image-generation jako silnik map** — odrzucone: te same
  fundamentalne problemy z siatką i spójnością co istniejące generatory obrazów
  (ChatGPT itp.); wysokie ryzyko "zero-to-one" bez gwarancji poprawy.
- **Działanie offline** — MVP nie zapewnia działania bez połączenia z internetem.
- **Pełna hierarchia Kampania→Sesja→Mapa z CRUD** — MVP operuje na pojedynczej
  encji Mapa; grupowanie w kampanie/sesje to praca po MVP (etap 2).
- **Integracja API z Roll20** — eksport pozostaje ręczny (pobranie pliku PNG
  do samodzielnego wgrania), bez automatycznego wgrywania do Roll20.
- **Dodatkowe motywy graficzne, pułapki, skarby, dekoracje na mapie** — MVP
  generuje tylko podstawowy układ pokoi/korytarzy z jednym tileset; rozszerzenia
  wizualne i treściowe to praca po MVP.
- Zapis mapy w bibliotece z tytułem należy wyłącznie do etapu 2.

## Open Questions

1. **Konflikt wymagań kompatybilności z filtrem PRD — zapis historyczny ze źródła:**

   > **Konflikt wymagań kompatybilności z filtrem PRD** — Eksport do Roll20 oraz
   > działanie w Chrome i Firefox pozostają ustalonymi wymaganiami produktu.
   > Filtr nazw produktów w `/10x-prd` blokuje ich wierne przeniesienie do PRD;
   > nie jest to niepewność dotycząca kompatybilności. Do rozstrzygnięcia przez
   > autora przed generowaniem PRD: sposób zachowania tych wymagań zgodnie
   > z regułami generatora. W tej korekcie nie zmieniamy skilla.

   Status: rozwiązany 2026-09-16 zatwierdzoną przez autora zmianą filtra.
   Ostrzeżenie z Quality cross-check w notatkach jest nieaktualne; wymagania
   kompatybilności zachowano w odpowiednich sekcjach tego PRD.
2. **Secondary success criterion** — „Nie ustalono osobnego kryterium
   drugorzędnego dla MVP.” Do rozstrzygnięcia przez autora: pozostawienie
   braku dodatkowego kryterium albo jego określenie; biblioteka pozostaje
   poza MVP. Termin rozstrzygnięcia nieustalony.
3. **Tworzenie kont i dostęp bez logowania** — Notatki określają login
   i płaskie role, ale nie sposób tworzenia kont ani zachowanie przy próbie
   dostępu bez zalogowania. Do rozstrzygnięcia przez autora; termin nieustalony.
4. **Wpływ parametrów rozmiaru i typu starcia na wynik** — Nie określono,
   jakie rozmiary oznaczają S/M/L ani jak typ starcia zmienia mapę w MVP.
   Historia FR-003 odkłada reguły różnicujące walki z bossem, podczas gdy
   FR-002 i reguła biznesowa zachowują typ starcia jako wejście.
   Do rozstrzygnięcia przez autora; termin nieustalony.
