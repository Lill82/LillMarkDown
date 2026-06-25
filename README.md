# LillMarkDown

Aplicació d'escriptori per a Windows que converteix fitxers PDF a Markdown.

Arrossegueu un PDF a la finestra (o seleccioneu-lo amb el botó d'exploració) i es genera un fitxer `.md` al costat del PDF original.

## Com funciona

- Extreu el text pàgina a pàgina amb [iText 7](https://github.com/itext/itext-dotnet), respectant l'ordre espacial.
- Detecta els títols de manera heurística:
  - Línia en majúscules de fins a 80 caràcters → títol de nivell 2.
  - Línia curta (fins a 60 caràcters), sense puntuació final i precedida d'una línia en blanc → títol de nivell 3.
  - La resta → paràgrafs.
- Separa les pàgines amb `---`.

## Requisits

- Windows 10/11 x64
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (només per compilar; l'executable publicat és autocontingut)

## Compilació

```powershell
# Compilar i executar (Debug)
dotnet run --project LillMarkDown

# Publicar un executable únic autocontingut (win-x64)
dotnet publish LillMarkDown -p:PublishProfile=win-x64
# Sortida: LillMarkDown/bin/publish/LillMarkDown.exe
```

## Llicència

[CC BY 4.0](LICENSE.md) © 2026 Lill82

L'executable inclou iText 7, que es distribueix sota la llicència AGPL-3.0.
