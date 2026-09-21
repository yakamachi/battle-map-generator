# Pomysł na projekt zaliczeniowy — Battle Map Generator dla D&D (DM Toolkit)

Ustalone w rozmowie "grillingowej" na podstawie wymagań z `projekt-kursowy-mniejszy-niz-myslisz.md`.

## Podsumowanie

| Kryterium | Opis |
|---|---|
| **Nazwa robocza** | Battle Map Generator dla D&D (DM Toolkit) |
| **Użytkownik** | Ja jako Mistrz Gry (DM), + narzeczona jako drugie konto |
| **Problem** | Brak szybkiego, powtarzalnego sposobu na wygenerowanie czytelnej, wyrównanej do siatki mapy bitewnej przed sesją D&D. Generatory AI (np. ChatGPT) zawodzą: siatka się nie zgadza, brakuje/nadmiar elementów, brak spójności graficznej. |
| **MVP (1. tydzień)** | Zaloguj się → wybierz kampanię/sesję → ustaw parametry (rozmiar mapy S/M/L, typ starcia: walka z bossem / potyczka poboczna) → wygeneruj mapę (algorytm BSP: pokoje + korytarze, renderowane z kafelków tileset) → zobacz wynikowy PNG → zapisz w bibliotece → pobierz PNG do użycia w Roll20 |
| **Dane / CRUD** | Encje: Kampania → Sesja → Mapa (parametry generowania + seed + cache PNG + notatka). Pełny CRUD: tworzenie, odczyt, edycja notatki/metadanych, usuwanie. |
| **Logika biznesowa (1 zdanie)** | System proceduralnie generuje spójny, wyrównany do siatki układ mapy bitewnej (pokoje + korytarze) na podstawie zadanych parametrów starcia i rozmiaru — deterministycznie, bez AI. |
| **Stack** | Backend: .NET (Web API) — mocna strona autora. Frontend: React. |
| **Kontrola dostępu** | Prosty login (np. e-mail/hasło), 2 konta na start (ja + narzeczona). |
| **Algorytm generowania** | Start: BSP (Binary Space Partitioning) + generowanie korytarzy. Rozważane w przyszłości: Wave Function Collapse (v2, jeśli zostanie czas). |
| **Assety graficzne** | Darmowy tileset **CC0**: [0x72's 16x16 Dungeon Tileset](https://0x72.itch.io/16x16-dungeon-tileset) (alternatywa: Kenney "Tiny Dungeon", też CC0). |
| **Testy** | 1) Test integracyjny API — wywołanie generate-map z parametrami zwraca poprawny/spójny układ mapy i zapisuje wpis w bibliotece. 2) Test e2e (Playwright) — logowanie → ustawienie parametrów → klik "generuj" → weryfikacja, że pojawia się poprawny PNG i da się go pobrać (bez błędu). |
| **CI/CD** | Pipeline (np. GitHub Actions) budujący backend + frontend i uruchamiający oba testy przy każdym pushu/PR. |
| **Eksport do Roll20** | PNG z konfigurowalnym rozmiarem kafelka w pikselach, tak by łatwo dopasować siatkę Roll20 po stronie użytkownika. |

## Rozszerzenia po MVP (nie w pierwszej wersji)
- Wave Function Collapse jako alternatywny/lepszy algorytm generowania.
- Dodatkowe motywy graficzne (nie tylko dungeon).
- Pułapki, skarby, dekoracje na mapie.
- Opcjonalny, tani LLM generujący krótki opis fabularny pomieszczenia/lochu (dodatek, nie rdzeń).

## Odrzucone / rozważane pomysły
- **Notatnik/todo** — zbyt mało logiki biznesowej (ryzyko "pustego CRUD").
- **Integracja z World of Warcraft** — brak jasnego "kto/po co", odrzucone przez autora.
- **Dom Ownership Tracker** (mapowanie mieszkania + zadania cykliczne z priorytetyzacją) — solidny plan B, mniejsze ryzyko technicznie, ale mniej osobistego "wow" niż generator map.
- **Przepis → Lista zakupów** (parsowanie tekstu przepisów, agregacja składników, kategoryzacja) — plan B, ryzyko w "brudnym" parsowaniu tekstu.
- **Generator map D&D — wariant z prawdziwym AI image-generation** — odrzucony: te same problemy co ChatGPT (siatka, spójność, elementy) są fundamentalnym ograniczeniem modeli dyfuzyjnych, wysokie ryzyko "zero-to-one".
- **Character/Encounter Balancer** (bilansowanie starć wg CR budget) — niezrealizowany plan B, czysta logika reguł gry.
- **Loot/Treasure Generator** — niezrealizowany plan B.
