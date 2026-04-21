# Temporary Leave System

**Summary:** The temporary leave system allows enlisted soldiers to suspend active service temporarily while preserving rank and progression. Players can request leave to handle clan business, return to the world map, and later rejoin their original lord or transfer to a different commander.

**Status:** ✅ Current  
**Last Updated:** 2026-04-21  
**Related Docs:** [Enlistment](../Core/enlistment.md), [Core Gameplay](../Core/core-gameplay.md)

---

## Index

- [Overview](#overview)
- [Requesting Leave](#requesting-leave)
- [The Leave State](#the-leave-state)
- [Returning to Service](#returning-to-service)
- [Transferring Service](#transferring-service)
- [Leave Expiration (Abandonment)](#leave-expiration-abandonment)

---

## Overview

Temporary Leave is a suspension of duty, not a discharge. It is **time-limited** (default 14 days). If you fail to return before the timer expires, you are marked as a deserter.

---

## Requesting Leave

You can request leave through three primary channels:
1.  **Muster Complete Menu**: After completing the muster sequence, a "Request Temporary Leave" option allows you to begin leave immediately after dismissal.
2.  **Enlisted Status Menu**: A "Request Leave" option is available in the main menu hub.
3.  **Lord Dialogue**: You can speak directly with your lord to request time away from the unit.

Approval is generally granted unless the unit is in a high-tempo situation (e.g., active siege or imminent battle).

**Muster Complete Option:**
- Available at the final muster stage after all outcomes are displayed
- Disabled during active siege or combat operations
- Provides a natural transition point to begin leave after receiving pay and resolving muster business
- If selected, leave begins immediately upon muster dismissal

---

## The Leave State

Once on leave:
-   **Visibility**: Your party becomes visible and active on the campaign map.
-   **Lord Tracking**: Your enlisted lord is automatically marked with a custom campaign marker so you can locate them to return to duty. The marker uses the lord's banner and follows their current party/settlement position on an hourly cadence, with immediate refresh when they enter or leave settlements.
-   **Movement**: You can move freely, trade, and engage in encounters as a vanilla mercenary/lord.
-   **Persistence**: Your rank, XP, reputation, and service record are frozen and preserved.
-   **Restrictions**: You cannot join other factions or enlist with foreign lords while on leave from your current post.

---

## Returning to Service

To resume your duties, you must find and speak with your lord.
-   Selecting "Reporting for duty" immediately restores your enlisted state.
-   The custom campaign marker on your lord is removed.
-   Your party is hidden, and you resume following the lord's army.

---

## Transferring Service

While on leave, you have the option to transfer your service to a **different lord within the same faction**.
-   Speak with another lord of your kingdom.
-   Select the option to transfer your service.
-   All progression (tier, XP, reputation) is maintained with the new commander.

---

## Leave Expiration (Abandonment)

If the leave timer reaches zero before you return:
-   **Service Terminated**: You are automatically discharged as having abandoned your post.
-   **Desertion Penalties**: Heavy relation hits with your lord and faction leader.
-   **Cooldown**: You may be blocked from re-enlisting with that faction for a significant duration.
-   **Gear**: Any Quartermaster-issued gear is considered stolen, and a crime rating is applied.
