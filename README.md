# LillMarkDown

[![build](https://github.com/Lill82/LillMarkDown/actions/workflows/build.yml/badge.svg)](https://github.com/Lill82/LillMarkDown/actions/workflows/build.yml)

Aplicació d'escriptori per a Windows que converteix fitxers PDF a Markdown.

Arrossegueu PDFs o carpetes a la finestra (o feu clic per seleccionar-los) i es genera un fitxer `.md` al costat de cada PDF.

## Descàrrega

Baixeu `LillMarkDown.exe` de la [darrera versió](https://github.com/Lill82/LillMarkDown/releases/latest). És un únic executable autocontingut: no cal instal·lar .NET.

## Què fa

- **Títols** segons la mida de la lletra (`#`, `##`, `###`). Les línies curtes en negreta o en majúscules es converteixen en un títol de nivell inferior.
- **Paràgrafs**: torna a unir les línies partides i les paraules tallades amb guionet a final de línia.
- **Llistes**: `•`, `–`, `1.`, `1)`, `a)` → llistes de Markdown.
- **Capçaleres, peus de pàgina i números de pàgina** repetits: s'eliminen.
- **Pàgines** separades amb `---`.
- **Carpetes**: en arrossegar una carpeta, es converteixen tots els PDFs que conté, també els de les subcarpetes.
- **Avisos**: indica els PDFs sense text (escanejats) i els protegits amb contrasenya.
- Per defecte no sobreescriu cap fitxer: si `document.md` ja existeix, crea `document (2).md`. Es pot canviar amb l'opció *Sobreescriu els fitxers .md existents*.

### Limitacions

- Els PDFs escanejats (només imatge) no tenen text; caldria passar-los abans per un OCR.
- Les taules i els textos a diverses columnes es converteixen línia a línia, sense estructura de taula.

## Compilació

Requereix el [SDK de .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                                   # compilar
dotnet test                                    # proves
dotnet run --project LillMarkDown              # executar
dotnet publish LillMarkDown -p:PublishProfile=win-x64
# Sortida: LillMarkDown/bin/publish/<versió>_<aaaammdd-hhmmss>/LillMarkDown.exe (una carpeta nova per compilació)
```

Per publicar una versió nova, creeu i pugeu una etiqueta (`git tag v1.1.0 && git push origin v1.1.0`). GitHub Actions compila l'executable i el publica a *Releases*.

## Llicència

Copyright © 2026 Lill82

Aquest programa és programari lliure: podeu redistribuir-lo i/o modificar-lo sota els termes de la [GNU Affero General Public License](LICENSE), tal com la publica la Free Software Foundation, versió 3 de la llicència.

Aquest programa es distribueix amb l'esperança que sigui útil, però SENSE CAP GARANTIA, ni tan sols la garantia implícita de COMERCIABILITAT o ADEQUACIÓ A UNA FINALITAT DETERMINADA. Vegeu la GNU Affero General Public License per a més detalls.

Utilitza [iText 7](https://github.com/itext/itext-dotnet), també sota llicència AGPL-3.0.
