---
project: "Battle Map Generator dla D&D (DM Toolkit)"
version: 2
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
narzędzia, jeśli pomysł się sprawdzi. Pierwsi znajomi DM-owie przetestowali
już MVP i dali feedback (2026-09-30); od tego momentu mogą sami założyć konto
(otwarta rejestracja).

## Success Criteria

### Primary

- Etap 1 działa end-to-end: DM zakłada konto i loguje się, ustawia parametry
  starcia (liczba pokoi, typ: zwykła potyczka / walka z bossem), generuje mapę, widzi
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
- **When** ustawia parametry starcia (liczba pokoi, typ) i klika "generuj"
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

- FR-001: DM może założyć konto i zalogować się (e-mail/hasło). Priority: must-have
  > Socratic: Kontrargument rozważony: "przy 2 znanych kontach login to zbędna
  > złożoność, prościej byłoby bez auth". Rozstrzygnięcie: zostaje login —
  > przyda się przy ewentualnym rozszerzeniu do innych DM-ów, i to też element
  > workflow, który autor chce przećwiczyć.
  > Aktualizacja 2026-09-30: otwarta rejestracja zamiast kont zakładanych przez
  > autora — znajomi DM-owie mają móc zacząć bez udziału autora; limit
  > generowań na konto ogranicza koszt na darmowym planie.

### Generowanie mapy

- FR-002: DM może ustawić parametry generowania mapy: liczbę pokoi (2–12, domyślnie 6) i typ starcia (potyczka / walka z bossem; przy walce z bossem także rozmiar bossa: Duży / Ogromny / Gigantyczny). Priority: must-have
  > Socratic: Kontrargument rozważony: "za mało parametrów — generator będzie
  > sztywny/mało użyteczny". Rozstrzygnięcie: zostaje minimalny zestaw w MVP,
  > rozbudowa parametrów to praca po MVP.
  > Aktualizacja 2026-09-30 (peer feedback): rozmiar S/M/L zastąpiony liczbą
  > pokoi — DM od razu wie, co dostanie, a rozmiar mapy wynika z liczby pokoi.
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

### Rozszerzenia generatora (peer feedback 2026-09-30, po M-1)

- FR-007: DM może wygenerować mapę z pętlami ("okrężny" loch): drużyna może ruszyć w dowolną stronę i obejść wszystkie pokoje bez cofania się tą samą drogą. Priority: nice-to-have
- FR-008: DM może wygenerować mapę z terenem: trudnym (ruch kosztuje podwójnie, reguła D&D 5e) oraz nieprzechodnim (np. przepaść na środku pokoju), który nie przerywa spójności mapy. Priority: nice-to-have

## Non-Functional Requirements

- Generacja mapy kończy się i pokazuje wynik użytkownikowi w rozsądnym,
  ograniczonym czasie — górna granica akceptowalna to 5 minut, celem jest
  wyraźnie szybciej.
- Dane mapy/kampanii danego konta nie są widoczne dla innych kont.
- Produkt działa poprawnie w najnowszych wersjach Chrome i Firefox.

## Business Logic

System proceduralnie generuje spójny, wyrównany do siatki układ mapy bitewnej (pokoje + korytarze) na podstawie zadanych parametrów starcia i rozmiaru — deterministycznie, bez AI.

Reguła konsumuje jako wejście: liczbę pokoi (2–12) i typ starcia (zwykła
potyczka / walka z bossem, a przy walce z bossem rozmiar bossa), podane przez
DM w formularzu. Mapa ma dokładnie tyle pokoi, ile wybrał DM, a jej rozmiar
wynika z liczby pokoi (w granicach limitu rozmiaru mapy). Walka z bossem dodaje
jeden pokój-arenę, wyraźnie większy od pozostałych i oznaczony jako arena
bossa; minimalna podłoga areny zależy od rozmiaru bossa: Duży (2×2 kratki)
→ 8×8, Ogromny (3×3) → 10×10, Gigantyczny (4×4+) → 12×12. Uzasadnienie:
boss, jego zasięg (2–3 kratki) i 4–6 postaci poruszających się o 6 kratek na
turę muszą mieć miejsce na flankowanie i trzymanie dystansu. Wyjściem jest gotowy,
grywalny układ mapy — pokoje i korytarze rozmieszczone spójnie i wyrównane do
siatki, wyrenderowane graficznie z zestawu kafelków. DM spotyka tę regułę w
momencie kliknięcia "generuj": zamiast projektować mapę ręcznie, opisuje
zamiar, a system zwraca gotowy wynik do ewentualnej regeneracji lub pobrania.

## Access Control

Login (e-mail/hasło) oraz otwarta samodzielna rejestracja (e-mail/hasło) —
decyzja 2026-09-30, zastępuje „2 konta na start”. Płaski model ról — każde
konto ma te same uprawnienia, nie ma podziału na właściciela kampanii vs gościa.
Niezalogowany użytkownik widzi tylko stronę logowania / rejestracji i nie może
wygenerować mapy — także z pominięciem formularza. Weryfikacja adresu e-mail
nie wchodzi do MVP (wymagałaby usługi wysyłki poczty). Każde konto ma limit
generowań, który chroni darmowy plan hostingu przed nadużyciem otwartej
rejestracji. Role pozostają płaskie, dopóki nie pojawi się konkretna potrzeba
rozróżnienia — np. płatny plan z większą liczbą generowań (zob. Non-Goals).

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
- **Druk mapy na papierze w skali 1 cal na kratkę (wiele stron)** — po MVP.
  Cel: korzystanie z map zarówno online (Roll20), jak i przy stole. MVP
  zapewnia pobranie jednego PNG w stałej, wysokiej rozdzielczości (140 px na
  kratkę), które nadaje się do Roll20 i do samodzielnego wydruku; podział na
  strony do druku to praca po MVP.
- **Pętle w układzie i teren (FR-007, FR-008)** — nice-to-have, poza M-1;
  czyste drzewo pokoi i korytarzy bez terenu wystarcza do kryterium sukcesu.
- **Wiele poziomów lochu w jednym generowaniu (połączonych schodami)** —
  odłożone (peer feedback 2026-09-30): wiele map w jednym wyniku
  i dopasowanie schodów między poziomami to duża zmiana zakresu.
- **Prosty wirtualny stół (tokeny, interakcja na mapie)** — odłożone (peer
  feedback 2026-09-30): stoi w sprzeczności z założeniem „pobierz PNG do
  Roll20”; to osobny produkt, nie rozszerzenie generatora.
- **Subskrypcje / płatne plany z większą liczbą generowań** — odłożone
  (2026-09-30): rozważane, jeśli liczba użytkowników urośnie; razem z nimi
  przejście na wyższy plan hostingu (decyzja billingowa, tylko człowiek).
- **Weryfikacja adresu e-mail przy rejestracji** — poza MVP.

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

   Status: rozwiązany 2026-09-30 — otwarta rejestracja e-mail/hasło, limit
   generowań na konto, niezalogowany widzi tylko logowanie / rejestrację
   (zob. Access Control, FR-001).
4. **Wpływ parametrów rozmiaru i typu starcia na wynik** — Nie określono,
   jakie rozmiary oznaczają S/M/L ani jak typ starcia zmienia mapę w MVP.
   Historia FR-003 odkłada reguły różnicujące walki z bossem, podczas gdy
   FR-002 i reguła biznesowa zachowują typ starcia jako wejście.
   Do rozstrzygnięcia przez autora; termin nieustalony.

   Status: rozwiązany 2026-09-30 (peer feedback) — S/M/L zastąpione liczbą
   pokoi 2–12; walka z bossem dodaje arenę skalowaną rozmiarem bossa
   (zob. FR-002, Business Logic).
