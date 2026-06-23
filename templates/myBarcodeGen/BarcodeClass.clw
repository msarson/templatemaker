! ============================================================================
!  BarcodeClass - implementation. Linear (1D) barcode encoder + drawing.
!  Port of the ZXing-validated C# reference (designer/BarcodeCore).
!
!  Clarion port notes (same as QRCodeClass):
!   - bare MEMBER + a module-level MAP so BUILTINS.CLW (LEN, BOX, SHOW, ...) resolves.
!   - modulus via SELF.Modulo(); INT() on truncating divides; no literal '%'.
!   - the value is CLIP'd so a space-padded fixed-length STRING is not encoded.
!
!  This file MUST be stored in ANSI (not UTF-8).
! ============================================================================
  MEMBER

  MAP
  END

  INCLUDE('BarcodeClass.INC'),ONCE

!=== construct / destruct ====================================================
BarcodeClass.Construct PROCEDURE()
  CODE
  SELF.Init()

BarcodeClass.Destruct PROCEDURE()
  CODE
  ! no reference members to dispose

!=== one-time lookup tables ==================================================
BarcodeClass.Init PROCEDURE()
  CODE
  IF SELF.Ready = 1 THEN RETURN.
  SELF.Wide = 3
  SELF.C39Set = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*'
  SELF.C39All = 'nnnwwnwnnwnnwnnnnwnnwwnnnnwwnwwnnnnnnnnwwnnnwwnnwwnnnnnnwwwnnnnnnnwnnwnwwnnwnnwnnnnwwnnwnnwnnnnwnnwnnwnnwnnwwnwnnwnnnnnn' & |
                 'nwwnnwwnnnwwnnnnnwnwwnnnnnnnnwwnwwnnnnwwnnnnwnnwwnnnnnnwwwnnwnnnnnnwwnnwnnnnwwwnwnnnnwnnnnnwnnwwwnnnwnnwnnnwnwnnwnnnnnnn' & |
                 'wwwwnnnnnwwnnnwnnnwwnnnnnwnwwnwwnnnnnnwnwwnnnnnwwwwnnnnnnnwnnwnnnwwwnnwnnnnnwwnwnnnnnwnnnnwnwwwnnnnwnnnwwnnnwnnnwnwnwnnn' & |
                 'nwnwnnnwnnwnnnwnwnnnnwnwnwnnwnnwnwnn'
  SELF.ItfAll = 'nnwwnwnnnwnwnnwwwnnnnnwnwwnwnnnwwnnnnnwwwnnwnnwnwn'
  SELF.C128All = '212222222122222221121223121322131222122213122312132212221213221312231212112232122132122231113222123122123221223211221132' & |
                 '221231213212223112312131311222321122321221312212322112322211212123212321232121111323131123131321112313132113132311211313' & |
                 '231113231311112133112331132131113123113321133121313121211331231131213113213311213131311123311321331121312113312311332111' & |
                 '314111221411431111111224111422121124121421141122141221112214112412122114122411142112142211241211221114413111241112134111' & |
                 '111242121142121241114212124112124211411212421112421211212141214121412121111143111341131141114113114311411113411311113141' & |
                 '1141313111414111312114122112142112322331112'
  SELF.EanL = '0001101001100100100110111101010001101100010101111011101101101110001011'
  SELF.EanG = '0100111011001100110110100001001110101110010000101001000100010010010111'
  SELF.EanR = '1110010110011011011001000010101110010011101010000100010010010001110100'
  SELF.EanPar = 'LLLLLLLLGLGGLLGGLGLLGGGLLGLLGGLGGLLGLGGGLLLGLGLGLGLGGLLGGLGL'
  SELF.Ready = 1

!=== helpers =================================================================
BarcodeClass.Modulo PROCEDURE(LONG a,LONG b)
  CODE
  RETURN a - INT(a/b)*b

BarcodeClass.AddRun PROCEDURE(LONG dark,LONG n)
i  LONG
  CODE
  LOOP i = 1 TO n
    IF SELF.NBars >= 4000 THEN RETURN.                       ! buffer guard (very long inputs)
    SELF.NBars += 1
    SELF.Bars[SELF.NBars] = dark
  END

BarcodeClass.AddBitStr PROCEDURE(STRING s,LONG cnt)
i  LONG
  CODE
  LOOP i = 1 TO cnt
    SELF.AddRun(CHOOSE(s[i]='1', 1, 0), 1)
  END

BarcodeClass.AddSym39 PROCEDURE(LONG idx)
p  STRING(9)
i  LONG
  CODE
  p = SELF.C39All[ (idx-1)*9+1 : (idx-1)*9+9 ]
  LOOP i = 1 TO 9
    SELF.AddRun(CHOOSE(SELF.Modulo(i,2)=1, 1, 0), CHOOSE(p[i]='w', SELF.Wide, 1))   ! odd element = bar
  END

!=== Code 39 =================================================================
BarcodeClass.Code39 PROCEDURE(*CSTRING data)
s     CSTRING(256)
i     LONG
idx   LONG
star  LONG
  CODE
  s = UPPER(CLIP(data))
  star = INSTRING('*', SELF.C39Set, 1, 1)
  SELF.NBars = 0
  SELF.AddSym39(star)                                        ! start *
  LOOP i = 1 TO LEN(s)
    idx = INSTRING(s[i], SELF.C39Set, 1, 1)
    IF idx = 0 OR s[i] = '*' THEN RETURN 0.                  ! invalid character
    SELF.AddRun(0, 1)                                        ! narrow inter-character gap
    SELF.AddSym39(idx)
  END
  SELF.AddRun(0, 1)
  SELF.AddSym39(star)                                        ! stop *
  RETURN 1

!=== Interleaved 2 of 5 ======================================================
BarcodeClass.Itf PROCEDURE(*CSTRING digits)
s   CSTRING(64)
i   LONG
k   LONG
b   STRING(5)
sp  STRING(5)
d1  LONG
d2  LONG
  CODE
  s = CLIP(digits)
  LOOP i = 1 TO LEN(s)
    IF s[i] < '0' OR s[i] > '9' THEN RETURN 0.
  END
  IF SELF.Modulo(LEN(s),2) = 1 THEN s = '0' & s.             ! pad to even
  SELF.NBars = 0
  SELF.AddRun(1,1); SELF.AddRun(0,1); SELF.AddRun(1,1); SELF.AddRun(0,1)            ! start nnnn
  LOOP i = 1 TO LEN(s) BY 2
    d1 = VAL(s[i]) - 48
    d2 = VAL(s[i+1]) - 48
    b  = SELF.ItfAll[ d1*5+1 : d1*5+5 ]                      ! bars carry the 1st digit
    sp = SELF.ItfAll[ d2*5+1 : d2*5+5 ]                      ! spaces carry the 2nd
    LOOP k = 1 TO 5
      SELF.AddRun(1, CHOOSE(b[k]='w',  SELF.Wide, 1))
      SELF.AddRun(0, CHOOSE(sp[k]='w', SELF.Wide, 1))
    END
  END
  SELF.AddRun(1, SELF.Wide); SELF.AddRun(0,1); SELF.AddRun(1,1)                     ! stop: wide,narrow,narrow
  RETURN 1

!=== Code 128 (auto Code B / Code C) =========================================
BarcodeClass.Code128 PROCEDURE(*CSTRING data)
s       CSTRING(256)
i       LONG
k       LONG
allDig  BYTE
nv      LONG
vv      LONG,DIM(300)
v       LONG
sum     LONG
cc      LONG
p       STRING(7)
plen    LONG
  CODE
  s = CLIP(data)
  allDig = 1
  LOOP i = 1 TO LEN(s)
    IF s[i] < '0' OR s[i] > '9' THEN allDig = 0; BREAK.
  END
  nv = 0
  IF allDig = 1 AND LEN(s) >= 2 AND SELF.Modulo(LEN(s),2) = 0
    nv += 1; vv[nv] = 105                                    ! Start C
    LOOP i = 1 TO LEN(s) BY 2
      nv += 1; vv[nv] = (VAL(s[i])-48)*10 + (VAL(s[i+1])-48)
    END
  ELSE
    nv += 1; vv[nv] = 104                                    ! Start B
    LOOP i = 1 TO LEN(s)
      cc = VAL(s[i])
      IF cc < 32 OR cc > 126 THEN RETURN 0.
      nv += 1; vv[nv] = cc - 32
    END
  END
  sum = vv[1]                                                ! checksum (mod 103)
  LOOP i = 2 TO nv
    sum += (i-1) * vv[i]
  END
  nv += 1; vv[nv] = SELF.Modulo(sum, 103)
  nv += 1; vv[nv] = 106                                      ! Stop
  SELF.NBars = 0
  LOOP i = 1 TO nv
    v = vv[i]
    IF v = 106
      p = SELF.C128All[ 637 : 643 ]; plen = 7
    ELSE
      p = SELF.C128All[ v*6+1 : v*6+6 ]; plen = 6
    END
    LOOP k = 1 TO plen
      SELF.AddRun(CHOOSE(SELF.Modulo(k,2)=1, 1, 0), VAL(p[k]) - 48)    ! odd element = bar
    END
  END
  RETURN 1

!=== EAN-13 / UPC-A ==========================================================
BarcodeClass.EanCheckDigit PROCEDURE(*CSTRING d12)
s  LONG
i  LONG
n  LONG
  CODE
  s = 0
  LOOP i = 1 TO 12
    n = VAL(d12[i]) - 48
    IF SELF.Modulo(i,2) = 1                                  ! 1-based odd position -> weight 1
      s += n
    ELSE
      s += n * 3
    END
  END
  RETURN CHR(48 + SELF.Modulo(10 - SELF.Modulo(s,10), 10))

BarcodeClass.UpcCheckDigit PROCEDURE(*CSTRING d11)
s  LONG
i  LONG
n  LONG
  CODE
  s = 0
  LOOP i = 1 TO 11
    n = VAL(d11[i]) - 48
    IF SELF.Modulo(i,2) = 1                                  ! 1-based odd position -> weight 3
      s += n * 3
    ELSE
      s += n
    END
  END
  RETURN CHR(48 + SELF.Modulo(10 - SELF.Modulo(s,10), 10))

BarcodeClass.Ean13 PROCEDURE(*CSTRING digits)
s     CSTRING(20)
i     LONG
d     LONG
first LONG
par   STRING(6)
  CODE
  s = CLIP(digits)
  LOOP i = 1 TO LEN(s)
    IF s[i] < '0' OR s[i] > '9' THEN RETURN 0.
  END
  IF LEN(s) = 12
    s = s & SELF.EanCheckDigit(s)
  ELSIF LEN(s) <> 13
    RETURN 0
  END
  SELF.HumanText = s
  SELF.NBars = 0
  first = VAL(s[1]) - 48
  par = SELF.EanPar[ first*6+1 : first*6+6 ]
  SELF.AddBitStr('101', 3)                                   ! left guard
  LOOP i = 2 TO 7
    d = VAL(s[i]) - 48
    IF par[i-1] = 'L'
      SELF.AddBitStr(SELF.EanL[ d*7+1 : d*7+7 ], 7)
    ELSE
      SELF.AddBitStr(SELF.EanG[ d*7+1 : d*7+7 ], 7)
    END
  END
  SELF.AddBitStr('01010', 5)                                 ! centre guard
  LOOP i = 8 TO 13
    d = VAL(s[i]) - 48
    SELF.AddBitStr(SELF.EanR[ d*7+1 : d*7+7 ], 7)
  END
  SELF.AddBitStr('101', 3)                                   ! right guard
  RETURN 1

BarcodeClass.UpcA PROCEDURE(*CSTRING digits)
s    CSTRING(20)
t    CSTRING(20)
i    LONG
res  BYTE
  CODE
  s = CLIP(digits)
  LOOP i = 1 TO LEN(s)
    IF s[i] < '0' OR s[i] > '9' THEN RETURN 0.
  END
  IF LEN(s) = 11
    s = s & SELF.UpcCheckDigit(s)
  ELSIF LEN(s) <> 12
    RETURN 0
  END
  t = '0' & s                                                ! UPC-A = EAN-13 with a leading 0
  res = SELF.Ean13(t)
  SELF.HumanText = s                                         ! but show the 12-digit UPC, not the 13
  RETURN res

!=== dispatch + drawing ======================================================
BarcodeClass.Build PROCEDURE(LONG pType,*CSTRING pValue)
  CODE
  SELF.Init()
  SELF.HumanText = CLIP(pValue)
  CASE pType
  OF BC:Code39;  RETURN SELF.Code39(pValue)
  OF BC:Code128; RETURN SELF.Code128(pValue)
  OF BC:ITF;     RETURN SELF.Itf(pValue)
  OF BC:EAN13;   RETURN SELF.Ean13(pValue)
  OF BC:UPCA;    RETURN SELF.UpcA(pValue)
  ELSE;          RETURN 0
  END

BarcodeClass.PaintBars PROCEDURE(SIGNED pImageFeq,LONG pDark,LONG pLight,LONG pQuiet,LONG pShowText)
ImgX     LONG
ImgY     LONG
imgW     LONG
imgH     LONG
unit     LONG
total    LONG
offX     LONG
barH     LONG
i        LONG
runStart LONG
  CODE
  GETPOSITION(pImageFeq, ImgX, ImgY, imgW, imgH)
  SETPENCOLOR(pLight)
  BOX(ImgX, ImgY, imgW, imgH, pLight)                        ! light field + quiet zone
  IF SELF.NBars <= 0 THEN RETURN.
  unit = INT(imgW / (SELF.NBars + 2*pQuiet))
  IF unit < 1 THEN unit = 1.
  total = unit * (SELF.NBars + 2*pQuiet)
  offX = ImgX + INT((imgW - total)/2) + pQuiet*unit          ! centre, leave a quiet zone each side
  barH = imgH
  IF pShowText = 1 THEN barH = imgH - 12.                    ! reserve a line for the human-readable text
  IF barH < 1 THEN barH = imgH.
  SETPENCOLOR(pDark)
  i = 1
  LOOP WHILE i <= SELF.NBars
    IF SELF.Bars[i] = 1
      runStart = i
      LOOP WHILE i <= SELF.NBars AND SELF.Bars[i] = 1
        i += 1
      END
      BOX(offX + (runStart-1)*unit, ImgY, (i-runStart)*unit, barH, pDark)     ! one BOX per dark run
    ELSE
      i += 1
    END
  END
  IF pShowText = 1 AND SELF.HumanText
    SETPENCOLOR(pDark)
    SHOW(ImgX + INT(imgW/2) - LEN(CLIP(SELF.HumanText))*2, ImgY + barH + 1, CLIP(SELF.HumanText))
  END

BarcodeClass.Draw PROCEDURE(SIGNED pImageFeq,LONG pType,*CSTRING pValue,LONG pDark,LONG pLight,LONG pQuiet,LONG pShowText)
  CODE
  IF SELF.Build(pType, pValue) = 0 THEN RETURN.              ! invalid value - leave the control unchanged
  SETTARGET(,pImageFeq)
  BLANK
  SELF.PaintBars(pImageFeq, pDark, pLight, pQuiet, pShowText)
  SETTARGET()
