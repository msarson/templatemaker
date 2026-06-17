#TEMPLATE(identifier,'identifier - Show Procedure Name on Ctrl+Shift+I - v1.0'),FAMILY('ABC')
#!-----------------------------------------------------------------------------!
#!  identifier  -  (c) 2026 Reddin Assessments                                 !
#!                                                                             !
#!  A GLOBAL (APPLICATION-scope) extension. With no per-procedure setup it      !
#!  alerts Ctrl+Shift+I on EVERY procedure that owns a window; pressing it pops  !
#!  a MESSAGE showing the current procedure name.                               !
#!                                                                             !
#!  Mechanism / corpus citations:                                             !
#!    * APPLICATION-scope extension injecting into the per-procedure embed      !
#!      %WindowManagerMethodCodeSection (proven by anytext.tpl:450).            !
#!    * EVENT:OpenWindow + EVENT:AlertKey are field-independent window events,   !
#!      routed through WindowManager.TakeWindowEvent (ABWINDOW.TPW:563/:1462).  !
#!      We inject at PRIORITY(2000) - below the framework's 2500 block - so our  !
#!      self-contained CASE EVENT() sits above the CYCLE/BREAK LOOP and never    !
#!      nests inside the framework's own CASE.                                  !
#!    * "Has a window" guard: WHERE(%Window).                                   !
#!    * CtrlShiftI EQUATE(0349H) - KEYCODES.CLW:492.                            !
#!    * ICON:Asterisk EQUATE - equates.clw (ICON:Information does not exist).   !
#!                                                                             !
#!  The procedure name is baked in at GENERATE time via the %Procedure symbol,  !
#!  so each procedure reports its own name. Local code only - no globals, so no !
#!  multi-DLL handling needed.                                                  !
#!-----------------------------------------------------------------------------!
#SYSTEM
  #EQUATE(%identifierTPLVersion,'1.00')
#!-----------------------------------------------------------------------------!
#EXTENSION(identifier,'identifier - Show Procedure Name (Ctrl+Shift+I)'),APPLICATION,HLP('~identifier.htm')
#SHEET
  #TAB('&General')
    #BOXED('About'),SECTION
      #DISPLAY('identifier for Clarion  v' & %identifierTPLVersion)
      #DISPLAY('Press Ctrl+Shift+I in any window to see the procedure name.')
    #ENDBOXED
    #PROMPT('&Disable this template',CHECK),%identifierDisable,DEFAULT(0),AT(10)
  #ENDTAB
#ENDSHEET
#!-----------------------------------------------------------------------------!
#! Alert the hotkey when the window opens; show the procedure name when it is
#! pressed. Self-contained CASE at the TOP of TakeWindowEvent; never RETURNs.
#!-----------------------------------------------------------------------------!
#AT(%WindowManagerMethodCodeSection,'TakeWindowEvent','(),BYTE'),PRIORITY(2000),WHERE(%identifierDisable=0 AND %Window),DESCRIPTION('identifier - Ctrl+Shift+I procedure name')
  CASE EVENT()
  OF EVENT:OpenWindow
    ALERT(CtrlShiftI)
  OF EVENT:AlertKey
    IF KEYCODE() = CtrlShiftI
      MESSAGE('Procedure: %Procedure','Identifier',ICON:Asterisk)
    END
  END
#ENDAT
