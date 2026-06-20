#TEMPLATE(myBackground,'myBackground - per-window background color / image - v1.00'),FAMILY('ABC')
#!-----------------------------------------------------------------------------------
#!  myBackground
#!  Roberto Renz - 2026
#!
#!  A single, self-contained APPLICATION-scope ABC extension that:
#!    1. Gives every window a GLOBAL DEFAULT background - a solid color OR an image -
#!       applied automatically at window open.
#!    2. Lets the user press Ctrl+Shift+B on ANY window to pop a small chooser and set
#!       a PERSONAL background for THAT window: pick a color (color dialog) or an image
#!       (file dialog), or revert to the global default.
#!    3. Stores each window's personal choice in an INI file (one [section] per
#!       procedure / window) and re-applies it on reopen. A stored personal background
#!       overrides the global default.
#!
#!  Self-contained: two helper procedures are defined IN the program module (short-form
#!  prototypes in %GlobalMap, long-form bodies in %ProgramProcedures). No external
#!  .inc/.clw required. EXE targets (helper bodies live in the program module).
#!
#!  VERIFIED corpus facts:
#!    Window solid color   : 0{PROP:Color}=rgb  (alias PROP:FillColor;  property.clw:246/248).
#!                           mo.clw:103/136 sets a window's color this way at run time.
#!    Window image (filled): 0{PROP:Tiled}=0; 0{PROP:Centered}=0; 0{PROP:WallPaper}=file
#!                           property.clw:277/278/279. Stretch-to-fill is the DEFAULT when
#!                           neither Tiled nor Centered is set (LanguageReference WALLPAPER).
#!                           Run-time use on window 0: ClaBarManager.CLW:121.
#!    Clear the image      : 0{PROP:WallPaper}=''   (mo.clw:102).
#!    Color picker         : COLORDIALOG(<title>,*? rgb),SIGNED,PROC - non-zero on OK (builtins.clw:583).
#!    File picker          : FILEDIALOG(<title>,*? name,<ext>,flag),BOOL,PROC - true on OK (builtins.clw:837).
#!    Hot key              : CtrlShiftB EQUATE(0342H)  (KEYCODES.CLW:485).
#!    COLOR:None = -1 (EQUATES.CLW:186); a COLOR prompt with no color uses -1 (ABBROWSE.TPW:319).
#!-----------------------------------------------------------------------------------
#SYSTEM
  #EQUATE(%myBackgroundTPLVersion,'1.00')
#!-----------------------------------------------------------------------------------
#EXTENSION(myBackground,'myBackground - per-window background color / image'),APPLICATION,HLP('~myBackground.htm')
#SHEET,ADJUST
  #TAB('&General')
    #BOXED('About'),SECTION
      #DISPLAY('myBackground for Clarion  v' & %myBackgroundTPLVersion)
      #DISPLAY('Default background for every window, plus a Ctrl+Shift+B per-window picker.')
    #ENDBOXED
    #BOXED('Options'),AT(,,250)
      #PROMPT('&Disable this template',CHECK),%mbgDisable,DEFAULT(0),AT(10)
      #PROMPT('Default background &color:',COLOR),%mbgDefColor,DEFAULT(-1),PROMPTAT(8),AT(96,,60)
      #PROMPT('Default background &image:',@s255),%mbgDefImage,DEFAULT(''),PROMPTAT(8),AT(96,,140)
      #PROMPT('&INI file name:',@s255),%mbgIni,DEFAULT('.\myBackground.INI'),REQ,PROMPTAT(8),AT(96,,140)
      #PROMPT('Enable &Ctrl+Shift+B picker',CHECK),%mbgHotKey,DEFAULT(1),AT(10)
      #DISPLAY('Color -1 = no default color.  Image wins over color when both are set.')
    #ENDBOXED
  #ENDTAB
  #TAB('&Instructions')
    #BOXED('How to use myBackground')
      #DISPLAY('SETUP (design time)')
      #DISPLAY('1. Add this extension ONCE, at the Global / Application level.')
      #DISPLAY('2. (Optional) On the General tab set a default background COLOR and/or')
      #DISPLAY('   a default background IMAGE used by every window that has no personal')
      #DISPLAY('   setting. Leave both empty for no default. Set the INI file name.')
      #DISPLAY('3. Generate and build the application.')
      #DISPLAY('')
      #DISPLAY('AT RUN TIME')
      #DISPLAY('- Every window opens with its stored personal background, or the global')
      #DISPLAY('   default if it has none.')
      #DISPLAY('- Press Ctrl+Shift+B on any window for the chooser:')
      #DISPLAY('     Background Color...   pick a solid color (color dialog)')
      #DISPLAY('     Background Image...   pick a picture file (stretched to fill)')
      #DISPLAY('     Use Default           drop this window''s personal setting')
      #DISPLAY('   The choice is applied at once and saved.')
      #DISPLAY('')
      #DISPLAY('STORAGE')
      #DISPLAY('- Each window is saved in its OWN INI section named after the procedure')
      #DISPLAY('   (e.g. [BrowseClients]) with Mode / Color / Image entries.')
      #DISPLAY('- On reopen the stored background is re-applied; a personal background')
      #DISPLAY('   overrides the global default. "Use Default" deletes that section.')
      #DISPLAY('')
      #DISPLAY('Image formats: BMP, JPG, GIF, PNG, ICO, WMF (whatever this build supports).')
    #ENDBOXED
  #ENDTAB
#ENDSHEET
#!-----------------------------------------------------------------------------------
#! Global MAP prototypes (SHORT FORM - survives MAP auto-indent; SKILL gotcha 1).
#!-----------------------------------------------------------------------------------
#AT(%GlobalMap),WHERE(%mbgDisable=0),DESCRIPTION('myBackground - helper prototypes')
myBackApply(STRING pKey, STRING pIni, LONG pDefColor, STRING pDefImage)
myBackChoose(STRING pKey, STRING pIni, LONG pDefColor, STRING pDefImage)
#ENDAT
#!-----------------------------------------------------------------------------------
#! Helper bodies, defined in the program module (%ProgramProcedures = DATA region, NOT
#! auto-indented, so written long-form at column 1; EXE-only embed). SKILL.
#!
#! Both helpers act on the CURRENT window, handle 0. myBackApply is called from inside
#! the caller's TakeWindowEvent (0 = that window). myBackChoose opens its OWN small
#! window to ask, CLOSEs it, THEN re-applies - after the CLOSE the caller window is the
#! current (top) window again, so 0 targets it correctly.
#!-----------------------------------------------------------------------------------
#AT(%ProgramProcedures),WHERE(%mbgDisable=0),DESCRIPTION('myBackground - helper bodies')
#!
myBackApply  PROCEDURE(STRING pKey, STRING pIni, LONG pDefColor, STRING pDefImage)
loc:Mode       STRING(1)
loc:Color      LONG
loc:Image      CSTRING(File:MaxFilePath+1)
  CODE
  !Decide what THIS window should show: its stored personal background, else the
  !global default. Mode 'C' = personal color, 'I' = personal image, blank = none.
  loc:Mode = GETINI(CLIP(pKey), 'Mode', '', pIni)
  CASE loc:Mode
  OF 'C'                                             !--- personal solid color ---
    loc:Color = GETINI(CLIP(pKey), 'Color', COLOR:None, pIni)
    0{PROP:WallPaper} = ''                           !drop any old image first
    0{PROP:Color}     = loc:Color
  OF 'I'                                             !--- personal image ---
    loc:Image = GETINI(CLIP(pKey), 'Image', '', pIni)
    0{PROP:Centered}  = 0                            !stretch to fill (not tiled/centered)
    0{PROP:Tiled}     = 0
    0{PROP:WallPaper} = CLIP(loc:Image)
  ELSE                                               !--- no personal setting: global default ---
    IF LEN(CLIP(pDefImage))                          !a default image wins when both are set (STRING: test length)
      0{PROP:Centered}  = 0
      0{PROP:Tiled}     = 0
      0{PROP:WallPaper} = CLIP(pDefImage)
    ELSIF pDefColor <> COLOR:None                    !else a default color
      0{PROP:WallPaper} = ''
      0{PROP:Color}     = pDefColor
    ELSE                                             !nothing configured: leave the standard look
      0{PROP:WallPaper} = ''
      0{PROP:Color}     = COLOR:None
    END
  END
  RETURN
#!
myBackChoose PROCEDURE(STRING pKey, STRING pIni, LONG pDefColor, STRING pDefImage)
loc:Color      LONG
loc:File       CSTRING(File:MaxFilePath+1)
loc:Acted      BYTE
window WINDOW('Window Background'),AT(,,164,86),CENTER,GRAY,SYSTEM,FONT('Segoe UI',9),DOUBLE
         PROMPT('Set the background for this window:'),AT(8,6,148,10),USE(?mbgPrompt)
         BUTTON('Background &Color...'),AT(8,20,148,16),USE(?mbgColor)
         BUTTON('Background &Image...'),AT(8,40,148,16),USE(?mbgImage)
         BUTTON('Use &Default'),AT(8,64,72,16),USE(?mbgDefault)
         BUTTON('&Close'),AT(84,64,72,16),USE(?mbgClose)
       END
  CODE
  OPEN(window)
  ACCEPT
    CASE ACCEPTED()
    OF ?mbgColor                                     !--- pick a solid color ---
      loc:Color = 0
      IF COLORDIALOG('Choose Background Color', loc:Color)
        PUTINI(CLIP(pKey), 'Mode',  'C',       pIni)
        PUTINI(CLIP(pKey), 'Color', loc:Color, pIni)
        PUTINI(CLIP(pKey), 'Image', '',        pIni)
        loc:Acted = 1
        BREAK
      END
    OF ?mbgImage                                     !--- pick an image file ---
      loc:File = ''
      IF FILEDIALOG('Choose Background Image', loc:File, |
                    'Images|*.bmp;*.jpg;*.jpeg;*.gif;*.png;*.ico;*.wmf|All Files|*.*', |
                    FILE:LongName)
        PUTINI(CLIP(pKey), 'Mode',  'I',            pIni)
        PUTINI(CLIP(pKey), 'Image', CLIP(loc:File), pIni)
        PUTINI(CLIP(pKey), 'Color', COLOR:None,     pIni)
        loc:Acted = 1
        BREAK
      END
    OF ?mbgDefault                                   !--- revert to the global default ---
      PUTINI(CLIP(pKey), , , pIni)                   !delete this window's whole section
      loc:Acted = 1
      BREAK
    OF ?mbgClose
      BREAK
    END
  END
  CLOSE(window)                                      !after CLOSE, 0 is the caller window again
  IF loc:Acted
    myBackApply(pKey, pIni, pDefColor, pDefImage)    !apply the new (or reverted) background now
  END
  RETURN
#ENDAT
#!-----------------------------------------------------------------------------------
#! TakeWindowEvent: window-level events arrive here with FIELD()=0. On EVENT:OpenWindow
#! apply the stored/default background and (optionally) ARM Ctrl+Shift+B - a key alert
#! MUST be armed for EVENT:AlertKey to fire. On EVENT:AlertKey for Ctrl+Shift+B pop the
#! chooser. Self-contained CASE at PRIORITY 2000 (above the framework CASE at 2500);
#! never RETURN, so normal event handling continues. WHERE(%Window) = only windowed
#! procedures (idiom from myFontChanger / myPixel).
#!-----------------------------------------------------------------------------------
#AT(%WindowManagerMethodCodeSection,'TakeWindowEvent','(),BYTE'),PRIORITY(2000),WHERE(%mbgDisable=0 AND %Window),DESCRIPTION('myBackground - apply background + arm Ctrl+Shift+B')
  CASE EVENT()
  OF EVENT:OpenWindow
    myBackApply('%Procedure', '%mbgIni', %mbgDefColor, '%mbgDefImage')
#IF(%mbgHotKey)
    ALERT(CtrlShiftB)
#ENDIF
#IF(%mbgHotKey)
  OF EVENT:AlertKey
    IF KEYCODE() = CtrlShiftB
      myBackChoose('%Procedure', '%mbgIni', %mbgDefColor, '%mbgDefImage')
    END
#ENDIF
  END
#ENDAT
#!-----------------------------------------------------------------------------------
#! INI key per window = '%Procedure' (the procedure name), substituted directly into the
#! output line - e.g. myBackApply('BrowseClients', ...). Procedure names are unique in an
#! app, so each window gets its own [Procedure] section: Mode=C|I, Color=rgb, Image=path.
#!-----------------------------------------------------------------------------------
#! End myBackground
#!-----------------------------------------------------------------------------------
