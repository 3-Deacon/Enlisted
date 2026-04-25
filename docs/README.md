# Enlisted - Bannerlord Soldier Career Mod

**Summary:** Transform Bannerlord into a soldier career simulator. Enlist with a lord, follow orders, manage reputation, and advance from recruit to commander.

**Version:** v0.9.0  
**Target Game:** Mount & Blade II: Bannerlord v1.3.13  
**Workshop:** [Steam Workshop Link - ID: 3621116083]

---

## Quick Start

**New to this project? Read these two files first:**
1. **[BLUEPRINT.md](BLUEPRINT.md)** - Project architecture, constraints, and coding standards
2. **[INDEX.md](INDEX.md)** - Complete documentation catalog with navigation guide

These provide full project context and help you find anything you need.

---

## Index

1. [What is Enlisted?](#what-is-enlisted)
2. [Documentation](#documentation)
3. [Quick Links](#quick-links)

---

## What is Enlisted?

Enlisted is a comprehensive mod that transforms Mount & Blade II: Bannerlord into a soldier career simulator. Instead of starting as a mercenary captain, you enlist with a lord's army and experience military life from the ground up.

**Core Features:**
- **Enlistment System** - Pledge service to any lord and join their army
- **Rank Progression** - Advance through 9 tiers from recruit to commander
- **Chain of Command** - Receive and complete orders from your lord
- **Reputation & Discipline** - Build trust with officers or face consequences
- **Dynamic Events** - Experience role-specific narrative events and social interactions
- **Company Needs** - Monitor and influence your unit's morale, readiness, and supplies
- **Quartermaster System** - Manage equipment allocation and quality
- **Combat Integration** - Participate in battles with your unit

---

## Documentation

### Getting Started
- **[Core Gameplay Guide](Features/Core/core-gameplay.md)** - Start here for an overview
- **[Installation & Setup](BLUEPRINT.md#build--deployment)** - How to install and configure

### For Players
- **[Features Index](Features/Core/index.md)** - All gameplay features explained
- **[Content Reference](Features/Content/storylet-backbone.md)** - Storylets, effects, triggers, activities, and current authoring rules

### For Developers
- **[Blueprint](BLUEPRINT.md)** - Project architecture and coding standards
- **[Developer Guide](DEVELOPER-GUIDE.md)** - How to build and modify the mod
- **[API Reference](Reference/native-apis.md)** - Bannerlord API notes

### Planning & Reference
- **[Complete Index](INDEX.md)** - All documentation files
- **[Plans Archive](superpowers/plans/archive/README.md)** - Retired and completed implementation plans

---

## Quick Links

**Build Command:**
```bash
dotnet build -c "Enlisted RETAIL" /p:Platform=x64
```

**Log Location:**
```
<BannerlordInstall>\Modules\Enlisted\Debugging\
```

**Decompiled Source Reference:**
```
C:\Dev\Enlisted\Decompile\
```

**Workshop ID:** 3621116083

---

**Questions?** Check the [Blueprint](BLUEPRINT.md) for technical details or the [Developer Guide](DEVELOPER-GUIDE.md) for getting started with development.

