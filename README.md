# Clarion Template Maker

Tooling to make Claude a **Clarion 12 template authoring professional** — for creating and editing the
`.tpl`/`.tpw` files that drive Clarion's Application Generator (AppGen).

This was built by studying the installed Clarion 12 template corpus:
- Shipped ABC + classic templates — `C:\clarion12\template\win\` (160 `.tpl`, 626 `.tpw`)
- Third-party / accessory templates — `C:\clarion12\accessory\template\win\` (AJE*, CapeSoft AnyFont/
  AnyText, ChromeExplorer, HotDates, KeepingTabs, Cryptonite, …)
- Official docs — `C:\clarion12\docs\TemplateLanguageReference.pdf`, `TemplateGuide.pdf`

## What was created

### 1. Skill — `clarion-template`
Location: `~/.claude/skills/clarion-template/`

A reusable knowledge pack Claude loads when working on any `.tpl`/`.tpw` file:
- `SKILL.md` — file types, the three-rule mental model (directive vs. literal, `#!` vs `!`,
  parse-time vs generate-time), the 80%-case extension skeleton, authoring workflow, correctness rules.
- `reference/directives.md` — full directive vocabulary (`#TEMPLATE`/`#PROCEDURE`/`#CONTROL`/
  `#EXTENSION`/`#CODE`/`#GROUP`, the `#PROMPT`/`#SHEET`/`#TAB`/`#BOXED` UI set, `%Symbol` state,
  control flow, `#AT`/`#EMBED` injection, `#GENERATE`/`#CREATE`/`#INSERT`) with real signatures.
- `reference/patterns.md` — the playbook: disable switch, multi-DLL externals + export lists, `ONCE`
  includes, Init/Kill lifecycle, multi-instance naming, `#GROUP` reuse, project files, custom embeds.
- `reference/examples.md` — three complete annotated templates (a procedure extension, an application
  extension, a value-returning group) plus a verification checklist.

### 2. Agent — `clarion-template-pro`
Location: `~/.claude/agents/clarion-template-pro.md`

A specialist subagent trained on the above. Use it for any template task — writing a new
procedure/control/extension/code/group template, modifying or debugging an existing one, explaining
directives, or designing the AppGen prompt UI and embed wiring. It reads the skill references and the
shipped corpus before writing, respects the parse-time/generate-time model, and predicts the generated
Clarion source so you know exactly what to verify.

## Repo layout

```
skills/clarion-template/        # the skill (SKILL.md + reference/)
agents/clarion-template-pro.md  # the specialist subagent
README.md
```

## Install

Copy the two folders into your Claude Code config (`~/.claude` on macOS/Linux,
`C:\Users\<you>\.claude` on Windows):

```sh
cp -r skills/clarion-template ~/.claude/skills/
cp agents/clarion-template-pro.md ~/.claude/agents/
```

Restart Claude Code (or start a new session) so the skill and agent are picked up.

## How to use

- Ask Claude to build/edit a Clarion template and it will pick up the `clarion-template` skill
  automatically (or invoke `/clarion-template`).
- For a focused deep task, delegate to the `clarion-template-pro` agent.

## Verifying a generated template

Claude cannot run AppGen. After it writes a template:
1. Copy the `.tpl` (+ `.tpw`/`.inc`/`.clw`) into the app's template/source path.
2. IDE → **Setup ▸ Template Registry ▸ Register** the `.tpl`.
3. Add the extension/control to a test procedure (or the app, for `APPLICATION` scope).
4. Fill prompts, **Generate**, and confirm the produced `.clw` compiles.
