#TEMPLATE(showLine,'showLine - Where-Am-I Hotkey v1.00'),FAMILY('ABC')
#!-----------------------------------------------------------------------------!
#!  showLine  -  (c) 2026 Reddin Assessments                                   !
#!                                                                             !
#!  A GLOBAL (APPLICATION-scope) extension. With no per-procedure setup it     !
#!  alerts Ctrl+Shift+P on EVERY procedure that owns a window; pressing it pops !
#!  a MESSAGE telling you WHERE you are: the procedure (the "code" you are in),  !
#!  the control that currently has focus (its field number + USE variable),     !
#!  and the thread number.                                                      !
#!                                                                             !
#!  Mechanism / corpus citations:                                              !
#!    * APPLICATION-scope extension injecting into the per-procedure embed      !
#!      %WindowManagerMethodCodeSection (proven by anytext.tpl:450).            !
#!    * EVENT:OpenWindow + EVENT:AlertKey are field-independent window events   !
#!      routed through WindowManager.TakeWindowEvent (ABWINDOW.TPW:563/:1462).  !
#!      We inject at PRIORITY(2000) - BELOW the framework's 2500 block - so our  !
#!      self-contained CASE EVENT() sits above the CYCLE/BREAK LOOP, never       !
#!      nesting inside the framework's own CASE (its CASE opens at 2500).        !
#!    * "Has a window" guard: WHERE(%Window).                                   !
#!    * CtrlShiftP EQUATE(0350H) - KEYCODES.CLW:499.                            !
#!    * FOCUS() returns the focused control's field number; feq{PROP:Use}       !
#!      (PROP:Use EQUATE 7A10H - PROPERTY.CLW:271) returns its USE variable      !
#!      name as a string.                                                       !
#!                                                                             !
#!  Multi-DLL: generates ONLY local procedure code (local data + method-body    !
#!  statements). No globals, no class instances => no EXTERNAL,DLL handling and  !
#!  no %DllExportList. The procedure name is baked in at GENERATE time, so each  !
#!  binary's procedures self-describe correctly.                                !
#!-----------------------------------------------------------------------------!
#SYSTEM
  #EQUATE(%showLineTPLVersion,'1.00')
#!-----------------------------------------------------------------------------!
#EXTENSION(showLineGlobal,'showLine - Where-Am-I Hotkey (Global)'),APPLICATION,HLP('~showLine.htm')
#SHEET,HSCROLL
  #TAB('&General')
    #BOXED('About'),SECTION
      #DISPLAY('showLine for Clarion  v' & %showLineTPLVersion)
      #DISPLAY('Press Ctrl+Shift+P in any window to see where you are.')
    #ENDBOXED
    #PROMPT('&Disable this template',CHECK),%showLineDisable,DEFAULT(0),AT(10)
  #ENDTAB
  #TAB('&Options')
    #ENABLE(%showLineDisable=0)
      #PROMPT('Show &focused control (field number + USE)',CHECK),%showLineFocus,DEFAULT(1),AT(10)
      #PROMPT('Message &title:',@s60),%showLineTitle,DEFAULT('showLine - Where Am I?')
    #ENDENABLE
  #ENDTAB
#ENDSHEET
#!-----------------------------------------------------------------------------!
#!  Local working variables (one set per procedure - no cross-proc collision).  !
#!-----------------------------------------------------------------------------!
#AT(%DataSection),WHERE(%showLineDisable=0 AND %Window)
showLine:Info        STRING(512)                           ! showLine: assembled message text
#IF(%showLineFocus)
showLine:Feq         LONG                                  ! showLine: focused control field equate
showLine:Use         STRING(255)                           ! showLine: focused control USE name
#ENDIF
#ENDAT
#!-----------------------------------------------------------------------------!
#!  Alert the hotkey when the window opens; build & show the report when the    !
#!  key is pressed (so focus reflects the moment of the keypress). Injected at   !
#!  the TOP of TakeWindowEvent (PRIORITY 2000), self-contained, never RETURNs.   !
#!-----------------------------------------------------------------------------!
#AT(%WindowManagerMethodCodeSection,'TakeWindowEvent','(),BYTE'),PRIORITY(2000),WHERE(%showLineDisable=0 AND %Window),DESCRIPTION('showLine - Where-Am-I hotkey')
  CASE EVENT()
  OF EVENT:OpenWindow
    ALERT(CtrlShiftP)
  OF EVENT:AlertKey
    IF KEYCODE() = CtrlShiftP
      showLine:Info = 'Procedure: %Procedure' & |
                      '<13,10>Binary: %Application (%ProgramExtension)' & |
                      '<13,10>Thread: ' & THREAD()
  #IF(%showLineFocus)
      showLine:Feq = FOCUS()
      IF showLine:Feq
        showLine:Use = showLine:Feq{PROP:Use}
        showLine:Info = CLIP(showLine:Info) & |
                        '<13,10>Focus FEQ: ' & showLine:Feq & |
                        '<13,10>Focus USE: ' & CLIP(showLine:Use)
      ELSE
        showLine:Info = CLIP(showLine:Info) & '<13,10>Focus: (no control has focus)'
      END
  #ENDIF
      MESSAGE(showLine:Info,'%showLineTitle',ICON:Asterisk)
    END
  END
#ENDAT
