!ABCIncludeFile
! ============================================================================
!  QRCodeClass - self-contained offline QR-code encoder for Clarion.
!
!  Byte mode, versions 1-10, ECC L/M/Q/H, automatic version + mask selection.
!  A line-for-line port of the ZXing-validated C# reference (QrCodeCore); the
!  encoder logic is identical, only reorganised into a CLASS so it compiles in
!  its own module (QRCodeClass.CLW) instead of bloating the program's global
!  procedure area. All working state lives in class members.
!
!  USAGE (handled for you by the myQRDraw template):
!    1. Put QRCodeClass.inc + QRCodeClass.clw on the Clarion redirection path
!       (e.g. the app folder or \clarion12\libsrc\win).
!    2. INCLUDE('QRCodeClass.INC'),ONCE   and declare ONE global instance:
!         QRCodeObj  QRCodeClass
!       The LINK attribute pulls QRCodeClass.CLW into the build automatically.
!    3. Encode + draw into a window IMAGE control:
!         QRCodeObj.Draw(?Image, loc:Value, 2, COLOR:Black, COLOR:White, 4)
!       or, for a report band, in the Before-Print embed:
!         IF QRCodeObj.BuildMatrix(loc:Value, 2)
!           SETTARGET(Report); QRCodeObj.Paint(?Image, ...); SETTARGET()
!         END
!
!  This file MUST be stored in ANSI (not UTF-8).
! ============================================================================

QRCodeClass CLASS,TYPE,MODULE('QRCodeClass.CLW'),LINK('QRCodeClass.CLW')
! ---- working data (sized for the largest supported symbol, v10 = 57x57) ----
Ready              BYTE                                       ! 1 once GF tables + capacity table are built
GFExp              LONG,DIM(512)                              ! GF(256) antilog
GFLog              LONG,DIM(256)                              ! GF(256) log  (GFLog[value+1])
FinderA            BYTE,DIM(11)                               ! 1:1:3:1:1 finder-like patterns (penalty rule 3)
FinderB            BYTE,DIM(11)
TEc                LONG,DIM(10,4)                             ! capacity table [version,ecc(1=L..4=H)]:
TG1B               LONG,DIM(10,4)                             !   EC-per-block, group-1 blocks/data,
TG1D               LONG,DIM(10,4)                             !   group-2 blocks/data
TG2B               LONG,DIM(10,4)
TG2D               LONG,DIM(10,4)
Cells              BYTE,DIM(57,57)                            ! module matrix (1=dark), [row+1,col+1]
Func               BYTE,DIM(57,57)                            ! function-module map (1=reserved)
N                  LONG                                       ! current dimension (17+4*version)
Ver                LONG                                       ! chosen version 1-10
Ecc                LONG                                       ! 1=L 2=M 3=Q 4=H
DLen               LONG                                       ! data byte count
TotalData          LONG                                       ! data codewords for version+ecc
CCBits             LONG                                       ! char-count field width (8 or 16)
NBits              LONG                                       ! bits appended so far
Bits               BYTE,DIM(3000)                             ! one byte per appended bit (0/1)
DataCW             BYTE,DIM(400)                              ! packed data codewords
DataLen            LONG
CW                 BYTE,DIM(400)                              ! interleaved data+EC codewords
CWLen              LONG
EcLen              LONG                                       ! EC codewords per block
BestMask           LONG                                       ! chosen mask 0-7
BlkData            BYTE,DIM(130)                              ! one data block (RS input)
BlkEcc             BYTE,DIM(31)                               ! one block's EC (RS output)
DataBlk            BYTE,DIM(8,130)                            ! all data blocks
EccBlk             BYTE,DIM(8,31)                             ! all EC blocks
BlkLenArr          LONG,DIM(8)                                ! data length of each block
NumBlocks          LONG
Align              LONG,DIM(3)                                ! alignment-pattern centres
NAlign             LONG
! ---- construct / destruct (auto-run) ---------------------------------------
Construct          PROCEDURE
Destruct           PROCEDURE
! ---- public methods --------------------------------------------------------
Draw               PROCEDURE(SIGNED pImageFeq,*CSTRING pValue,LONG pEcc,LONG pDark,LONG pLight,LONG pQuiet)
BuildMatrix        PROCEDURE(*CSTRING pValue,LONG pEcc),BYTE
Paint              PROCEDURE(SIGNED pImageFeq,LONG pDark,LONG pLight,LONG pQuiet)
! ---- internal methods ------------------------------------------------------
Init               PROCEDURE()
Tbl                PROCEDURE(LONG v,LONG e,LONG ec,LONG g1b,LONG g1d,LONG g2b,LONG g2d)
GfMul              PROCEDURE(LONG a,LONG b),LONG
Modulo             PROCEDURE(LONG a,LONG b),LONG
MaskBit            PROCEDURE(LONG mask,LONG r,LONG c),BYTE
ApplyMask          PROCEDURE(LONG mask)
Finder             PROCEDURE(LONG r,LONG c)
Alignment          PROCEDURE(LONG r,LONG c)
SetCell            PROCEDURE(LONG r,LONG c,LONG v)
GetM               PROCEDURE(LONG r,LONG c),BYTE
GetF               PROCEDURE(LONG r,LONG c),BYTE
AppendBits         PROCEDURE(LONG val,LONG n)
ReedSolomon        PROCEDURE(LONG dataLen,LONG ecLen)
FormatInfo         PROCEDURE(LONG mask)
Penalty            PROCEDURE(),LONG
LineRuns           PROCEDURE(LONG idx,LONG isRow),LONG
MatchFinder        PROCEDURE(LONG a,LONG b,LONG isRow),BYTE
                   END
