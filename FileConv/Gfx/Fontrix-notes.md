# Fontrix Fonts #

File types:

- BIN / $6400: Fontrix Font File

Primary references:

- Reverse engineering

## Fonts ##

Each font contains between 1 and 94 characters.

Character data is stored one bit per pixel, where 0 indicated background and 1 is foreground. Data
for each glyph is stored in byte order from left to right, top to bottom,  with the left-most pixel
of each byte stored in the LSB.  Glyph data is referenced by a pointer into the file for each
character.  Fonts need not contain all characters; there are values identifying  the first character
and number of characters included in the file.  Regardless of how many characters are included,
there are always 96 data pointers, representing ASCII values 32 through 127, although the first and
last characters are never defined nor used by Fontrix. Following the pointers to the glyph data is
a pointer to the first byte beyond any data.  This may point beyond the end of file.

The glyph data is always mapped sequentially
and the width in bytes of a character can be determined by the difference between its data offset
and that of the following character, divided by the given glyph height.

Offsets $0E2-$141 appear to potentially be the individual character widths for proportional fonts,
although further research needs to be done to confirm this.

The file format is:

```
+$000/1:   Unknown byte
+$001/15:  Font Name, encoded in low ASCII
+$010/1:   Number of characters in file
+$011/1:   ASCII value of first encoded character
+$012/1:   Proportional flag.  0 = Non-proportional/1 = Proportional
+$013/1:   Non-Proportional font glyph width. For proportional fonts this seems to be an average
+$014/1:   Font height
+$015/1:   Unknown byte
+$016/2:   Font file size. This is little-endian encoded
+$018/3:   Identifier bytes.  All Fontrix fonts have a fixed sequence of either $90/$F7/$B2 or
             their ones-compliment values $6F/$08/$4D here
+$01B/5:   Unknown bytes
+$020/192: Character data offsets. 96 little-endian offsets from start of file. There are offsets
             for characters 32 (space) and 127 (del) although they are not used in Fontrix
+$0E0/2:   Pointer to the byte beyond the end of glyph data
+$0E2/96:  Widths of glyphs in pixels. Required for proportional, optional for non-proportional.
+$142/62:  Unknown bytes.  Usually 0 filled.
+$180/nnn: Character glyph data.  
```