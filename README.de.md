# Zogrid

**Arbeitsplätze · Serverraum · Sitzplanverwaltung**

Open Source · Kostenlos · Kommerziell nutzbar · Kein Verkauf · Vollständig offline · 7 Sprachen

**Andere Sprachen / Other languages：**
[简体中文](README.md) · [繁體中文](README.zh-Hant.md) · [English](README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Français](README.fr.md) · [Deutsch](README.de.md)

---

Excel merkt sich Daten, aber nicht, *wer am Fenster in der dritten Reihe sitzt*.
Aufkleber markieren Plätze, müssen aber bei jedem Wechsel neu geklebt werden.

Zogrid verbindet beides: Der Bildschirm zeigt genau das, was an der Wand hängt, und jeder Platz trägt seine Person und ihre Geräte.
Ein paar hundert Rechner im Schulungsraum, eine ganze Büroetage, ein Internetcafé — ein Plan genügt.

![Raumplan](截图/room.png)

## Für wen

**Schul- und Ausbildungsräume** — Dutzende bis hunderte Rechner. Welches Modell, welche IP, welcher Rechner defekt und frei ist: alles auf einen Blick.

**Büroetagen** — farblich getrennte Abteilungen, Chefplätze über zwei Felder, Nummern wie in der Realität. Ein Personen- oder Gerätewechsel ist eine Feldänderung.

**Internetcafés / Vor-Ort-Support** — Platznummern, Modelle, LAN-/WAN-IPs und MACs auf einem Bildschirm. Freie Plätze werden unten live gezählt.

---

## Kernfunktionen

### 1. Den Raum so zeichnen, wie er ist

Der Screenshot oben ist das Panel „Raumeigenschaften“: Name, Spalten und Reihen, Boden- und Rasterfarbe.

- Unbegrenzt Räume, freie Namen: `3F-Raum A`, `2F-Finanzen`, `Café Erdgeschoss`
- Spalten und Reihen frei wählbar, Farben anpassbar, helle und dunkle Themen
- **Raumplan drucken**: Systemdrucker, Schwarzweiß, A4 hoch, automatisch skaliert und zentriert, Kopfzeile mit Modellübersicht
- Räume sperrbar: Plan fixiert, Platzinhalte bleiben editierbar

### 2. Eigene Blockformen bauen

![Blockbibliothek](截图/library.png)

- Fertige Blöcke mit 2, 3 Plätzen und „mit Chefplatz“
- Eigene Formen: beliebig viele Reihen × Spalten, ein Feld = ein Platz; Chefplatz belegt zwei Felder
- Farbe je Block zur Abgrenzung von Abteilungen
- Reihenfolge der Bibliothek per Ziehen änderbar

### 3. Ablegen, wo es hingehört

![Ziehen und Ablegen](截图/layout.png)

- Block aus der Bibliothek in den Raum ziehen, er rastet am Raster ein
- Freie Anordnung: an der Wand, Mittelgang, L-Form, Insel
- Rechtsklick zum **Fixieren** fertiger Blöcke

### 4. Strg + Platz ziehen: Personen und Geräte wandern mit, die Portnummer bleibt an der Wand

Beim Umsetzen geht meist nicht das Verschieben schief, sondern die Frage, **ob die Portnummer mitwandert**.

Zogrid-Regel: **Strg gedrückt halten und einen Platz auf einen anderen ziehen — „Person + Geräte“ tauschen komplett, die Portnummer bleibt fest an der Wand.**

| Wandert mit (gehört zur Person) | Bleibt stehen (gehört zur Wand) |
| --- | --- |
| Name, Personalnummer, PC-Modell, Monitor-Modell, IP, MAC, Notiz | Portnummer |

Warum diese Trennung: Die Portnummer ist die Stelle, an der das Kabel aus der Wand kommt. Personen und Geräte wechseln, die Dose bleibt. Kein Umstecken im Rack, keine Korrektur im Verzeichnis.

Details:
- Chefplatz, normaler Platz und leeres Feld sind beliebig tauschbar; ein Fehlgriff lässt sich zurückziehen
- **Strg funktioniert auch bei gesperrtem Raum** (die Sperre betrifft den Plan, nicht das Personal)
- Die Statuszeile bestätigt: „Name/Modell/Monitor/Personalnr./IP/MAC sind mitgezogen, die Portnummer blieb“

### 5. Eine Karteikarte pro Platz

![Platzdetails](截图/cell-edit.png)

- Name, Portnummer, PC-Modell (max. 4), Monitor-Modell (oft für die Nebenstelle genutzt), Personalnummer
- **Duales Netz** aktivieren, um LAN und WAN mit eigener IP und MAC zu erfassen
- Gerätedetails (IP / MAC / Notiz) in Bearbeiten oder Nur-Lesen
- Der Platz zeigt nur das Wesentliche, Details stehen in der Karte

### 6. Alles finden

![Suche](截图/search.png)

- Suche nach Name, IP, MAC, Modell, Personalnummer oder Portnummer, während der Eingabe
- Treffer blinken rosa
- Suche, Modell und freie Plätze sind **gegenseitig exklusiv**
- Esc löscht die Suche

### 7. Überblick behalten

![Statistik](截图/stats.png)

- Live-Zusammenfassung unten: `4 Modelle · 9 Geräte · 3 frei`
- Eine Karte je Modell mit Gesamt- und Frei-Zahl
- **Klick auf ein Modell** hebt alle seine Plätze hervor; **Klick auf Frei** nur Plätze mit Gerät ohne Person
- Eigene Textfarbe je Modell

### 8. Einen ganzen Raum aus einer Tabelle füllen

![Datenmenü](截图/template.png)

- **Daten → Leere Vorlage laden**: 8 Spalten — Name / Personalnr. / LAN-WAN / MAC / IP / Notiz (Modell) / Portnummer / Monitor-Modell
- Im neuen Raum nur die Namen eintragen
- **Arbeitsmappe importieren → Auf Plätze übertragen** füllt IP, MAC, Modell, Port und Monitor in Sekunden
- Leere Zellen überschreiben keine bereits geprüften Daten
- **Personalliste exportieren** erzeugt dasselbe Format für Korrektur und Reimport

---

## Außerdem

- **7 Oberflächensprachen**: 简体中文, 繁體中文, 日本語, 한국어, English, Français, Deutsch. Umschalten über „Sprache“ in der Symbolleiste, die App startet neu
- **Spaltennamen folgen der Sprache**: chinesische Oberfläche = chinesische Kopfzeile, andere Sprachen = englische Kopfzeile, **beide sind importierbar**
- **Vollständig offline**: kein Netz, kein Login, keine Übertragung
- **Eine einzelne exe**: läuft auch vom USB-Stick
- **Eine Datendatei**: `Dokumente\Zogrid\zogrid.json`
- **Dunkle Oberfläche**, angenehm bei Serverraumbeleuchtung
- **Erststart-Hilfe**: Bibliothek und Raumeigenschaften öffnen sich, „Neuer Raum“ pulsiert

## In drei Schritten

1. **Raum anlegen** — F2, Name, Spalten und Reihen
2. **Plätze setzen** — F1, Blöcke ziehen, Namen eintragen
3. **Daten einspielen** — Daten → leere Vorlage → Importieren → Auf Plätze übertragen

## Tastenkürzel

| Taste | Funktion |
| --- | --- |
| F1 | Bibliothek ein-/ausblenden |
| F2 | Raumeigenschaften |
| F3 | Statistik |
| F4 | Platzdetails |
| F5 | Alles anzeigen |
| Mausrad | Zoom |
| Mitteltaste | Verschieben |
| Pfeiltasten | 60 px verschieben |
| **Strg + Platz ziehen** | Zwei Plätze tauschen |
| Entf | Block löschen |
| Esc | Suche leeren / Info schließen |

## Systemanforderungen

- Windows 7 oder neuer (Windows 10 / 11 ohne Zusatzinstallation)
- .NET Framework 4.0+ (in Windows 10/11 enthalten)
- Keine Installation, kein Netzwerk, keine Administratorrechte

## Lizenz

**Copyright © 2026 Michael. Alle Rechte beim Autor.**

- **Open Source und kostenlos**: Der Quellcode ist offen und darf angesehen, gebaut und genutzt werden
- **Kommerzielle Nutzung erlaubt**: Schulen, Firmen und Internetcafés dürfen es kostenlos nutzen
- **Verkauf verboten**: keine Gebühren, kein Verkauf, kein Bündelverkauf und keine Monetarisierung in irgendeiner Form
- Diese Einschränkung gilt auch für spätere Fassungen, Forks sowie Mac- und andere Plattformversionen
- **Keine Gewährleistung**: bereitgestellt wie besehen; der Autor haftet nicht für Schäden aus der Nutzung

Vollständige Bedingungen in [LICENSE.txt](LICENSE.txt).

---

Autor: Michael · Zogrid · 2026
