# Clarion Template — Annotated Complete Examples

Three real templates dissected. Read the original alongside: paths are given. Line references are
approximate (the corpus drifts between releases) — open the file to confirm.

---

## Example A — A complete `#EXTENSION` (procedure scope)

Source: `C:\clarion12\template\win\CONTROL.TPW`, the `ViewFormActions` extension. This is the model to
copy for "prompt for options, then inject code at procedure embed points." Annotated:

```
#EXTENSION(ViewFormActions, 'Configure View Only form mode'),PROCEDURE,HLP('~ViewFormActions.htm')
#!  ^ registers a procedure-scoped extension the developer can add to any Form.
#!
#PROMPT('&Force View Only Mode:', CHECK),%ForceViewOnly,AT(10),DEFAULT(%False)
#!  ^ one checkbox symbol.
#BUTTON('View &Action for ' & %Control),FROM(%Control, %Control & ' - ' & %ViewAction),INLINE
#!  ^ a per-control list: one row per control on the window, each row stores %ViewAction.
  #PROMPT('&View Action:', DROP('None|Hide|Disable|ReadOnly|Set Properties')),%ViewAction,DEFAULT(%GetDefaultViewAction(%Control))
  #BOXED('View Action Property Settings'),WHERE(%ViewAction = 'Set Properties')
#!    ^ nested box appears only when the row's action is 'Set Properties'.
    #BUTTON('Property Assignments'),MULTI(%PropertyAssignments, '{ '&%CtrlProperty&' } = '&%CtrlValue),INLINE,AT(,,,50)
      #PROMPT('&Property:', @s80),%CtrlProperty,REQ
      #PROMPT('New &Value:', @S255),%CtrlValue,REQ
    #ENDBUTTON
  #ENDBOXED
#ENDBUTTON
#!
#AT(%ProcedureInitialize),PRIORITY(3500),WHERE(%ForceViewOnly)
GlobalRequest = ViewRecord
#ENDAT
#!  ^ only emitted when the checkbox is on; sets view mode at init.
#!
#AT(%BeforeAccept),DESCRIPTION('Configure Controls for View Only Mode')
DO FORM:ViewRecordMode
#ENDAT
#!
#AT(%ProcedureRoutines)
!----------------------------------------------------------------
FORM:ViewRecordMode  ROUTINE
  IF LocalRequest = ViewRecord THEN
     #FOR(%Control)
#!     ^ generate-time loop over every control, branching on its stored action.
       #CASE(%ViewAction)
       #OF('Disable')
    DISABLE(%Control)
       #OF('Hide')
    HIDE(%Control)
       #OF('ReadOnly')
    %Control{PROP:ReadOnly} = TRUE
       #OF('Set Properties')
         #FOR(%PropertyAssignments)
    %Control{%CtrlProperty} = %CtrlValue
         #ENDFOR
       #ENDCASE
     #ENDFOR
  END
!----------------------------------------------------------------
#ENDAT
```

Why it's exemplary: prompts drive a per-control list (`FROM(%Control,...)`), nested options reveal
conditionally (`WHERE`), and generation walks the AppGen control collection (`#FOR(%Control)`) to emit
a runtime ROUTINE. Note the literal lines (`DISABLE(%Control)`) are indented to real Clarion columns,
while the `#` directives sit at column 1.

---

## Example B — A compact accessory extension (application scope)

Pattern assembled from `AJEUtilityClass.tpl` / `AnyFont.tpl` / `ChromeExplorer.tpl`. This is the
copy-paste starting point for a new third-party-style global tool:

```
#TEMPLATE(MyTools,'My Tools - Version 1.00'),FAMILY('ABC','CW20')
#!-----------------------------------------------------------------------------!
#!   MyTools - (c) 2026 Your Name - https://example.com                        !
#!-----------------------------------------------------------------------------!
#SYSTEM
  #EQUATE(%MyToolsTPLVersion,'1.00')
#!
#EXTENSION(ActivateMyTools,'Activate My Tools'),APPLICATION,HLP('~MyTools.htm')
#SHEET,HSCROLL
  #TAB('General')
    #BOXED('About'),SECTION
      #DISPLAY('MyTools for Clarion  v' & %MyToolsTPLVersion)
    #ENDBOXED
    #PROMPT('&Disable this template',CHECK),%MyToolsDisable,DEFAULT(0),AT(10)
  #ENDTAB
  #TAB('Settings')
    #PROMPT('Global &class instance:',@s40),%MyToolsObject,DEFAULT('MyTools'),REQ
    #PROMPT('&Connection string:',EXPR),%MyConnString,DEFAULT('')
  #ENDTAB
#ENDSHEET
#!
#AT(%AfterGlobalIncludes),WHERE(%MyToolsDisable=0)
INCLUDE('MyTools.INC'),ONCE
#ENDAT
#!
#AT(%GlobalData),WHERE(%MyToolsDisable=0)
  #IF(%MultiDLL=0 OR %RootDLL=1)
%MyToolsObject       MyToolsClass
  #ELSE
%MyToolsObject       MyToolsClass,EXTERNAL,DLL(dll_mode)
  #ENDIF
#ENDAT
#!
#AT(%ProgramSetup),PRIORITY(5000),WHERE(%MyToolsDisable=0)
%MyToolsObject.Init()
  #IF(%MyConnString)
%MyToolsObject.SetConnection(%MyConnString)
  #ENDIF
#ENDAT
#!
#AT(%ProgramEnd),WHERE(%MyToolsDisable=0)
%MyToolsObject.Kill()
#ENDAT
#!
#AT(%DllExportList),WHERE(%ProgramExtension='DLL' AND %RootDLL=1 AND %MultiDLL=1)
 #INSERT(%ExportClassesPR,'MyTools.Inc')
 $%MyToolsObject     @?
#ENDAT
```

The accompanying `MyTools.INC`/`MyTools.CLW` (a normal Clarion `CLASS`) is hand-written and shipped with
the template; the template's job is only to declare the instance and wire the lifecycle.

---

## Example C — A value-returning `#GROUP` and its use

Source pattern: `KeepingTabs.tpl`. Groups are your subroutines; they keep the `#AT` blocks readable.

```
#GROUP(%StripQFromControl)
  #IF(SLICE(%Control,1,1)='?')
    #RETURN(SUB(%Control,2,LEN(CLIP(%Control))))   #! drop a leading '?'
  #ELSE
    #RETURN(%Control)
  #ENDIF
#!
#GROUP(%MakeBCFilename,%pCount,%pPrefix),AUTO       #! parameters with AUTO (run during generation)
  #EQUATE(%BCFName,SLICE(%Program,1,INSTRING('.',%Program,1,1)-1))
  #RETURN(UPPER(CLIP(SUB(%BCFName,1,5))) & %pPrefix & %pCount & '.CLW')
```

Used in a declaration line and a file split, respectively:
```
#SET(%ControlName,%StripQFromControl())
#SET(%FDFilename,%MakeBCFilename(%FDCount,'BC'))
```

---

## How to verify a template you wrote

You cannot run AppGen. Hand the developer these steps:

1. Copy the `.tpl` (+ any `.tpw`, `.inc`, `.clw`) into the app's template/source path.
2. In the IDE: **Setup ▸ Template Registry ▸ Register**, pick the `.tpl`, confirm the family appears.
3. Add the extension/control to a test procedure (or app, for `APPLICATION` scope).
4. Fill the prompts, **Generate** the app, and open the produced `.clw`.
5. Confirm: includes present once, the instance declared (and `EXTERNAL,DLL` in non-root DLLs),
   `Init`/`Kill` in place, and the project **compiles**. Diff against the expected output you predicted.

Predict the generated source in your answer so the developer knows exactly what to look for.
