# RestaurantLoop 🍔

> A conveyor-and-queue puzzle prototype made by a 5-person team during our **UDO Games internship**. We had about 2.5 weeks (20 August – 4 September) to build it. In the game, you feed a hungry crowd with food that rides around a conveyor belt. Feed everyone, and the restaurant becomes empty.

This is a prototype, and there is a lot of room to improve it.

---

## 👥 Team

| Name | Role |
| :--- | :--- |
| **Onur İnal** | Developer |
| **Hazar Kılıç** | Developer |
| **Enes Bozdemir** | Game Designer & Developer |
| **Bengisu** | Artist |
| **Merve** | Artist |

---

## 🎬 Gameplay Preview

| Level 1 | Level 28 |
| :---: | :---: |
| <img src="Media/Level1.gif" width="300"> | <img src="Media/Level28.gif" width="300"> |
| **Simple and easy.** Good for learning the basics. | **Medium-hard.** Bigger crowd and **timed customers**. |

---

## 🎓 What We Learned

- **Teamwork in a short time:** Five people and 2.5 weeks. We had to share work clearly and make decisions fast.
- **Data-driven levels:** Levels are ScriptableObjects, so designers can create and change levels without writing code. We also made a small editor tool to generate levels.
- **Events and decoupled code:** Systems talk to each other through events, so the UI and gameplay do not depend on each other too much.
- **Animation and path tools:** We used DOTween for animation and Dreamteck Splines to move food along the conveyor.
- **Mobile build:** We built and tested the game for Android with simple touch input.

---

## 🎮 What is RestaurantLoop?

A crowd of customers waits in the middle of the screen. Each customer wants one food, shown in an **order balloon**. A conveyor belt goes around the crowd.

The only input is a **tap**.

### How to Play

1. **Tap** a food stack at the **front row** of the queue. It goes onto the conveyor.
2. The stack moves around the belt. When it passes a **matching customer at the edge of the crowd**, it serves them.
3. The customer eats, jumps happily, and leaves. A new customer from the middle moves to the edge.
4. When a stack is empty, it disappears.
5. If a stack finishes its lap and still has food, it goes to the **rack**. Tap it there to send it around again.

### Important Rules

- Only customers at the **edge** of the crowd can be served.
- The conveyor can hold a limited number of stacks (**5** at the start). If it is full, you must wait.
- The rack has **5 slots**.
- Food types: 🍔 Burger, 🍟 Fries, 🥩 Beef, 🥤 Drink, 🍣 Sushi, 🍰 Cake.

---

## 🏆 Win and Lose

**You win** when every customer has been served.

**You lose** when:

- ❌ A stack finishes its lap, but the **rack is full**, so it has nowhere to go.
- ❌ A **timed customer** runs out of patience. These customers have a timer. If it reaches zero, the level fails.

---

## ⚡ Power-Ups

Power-ups help when you are stuck. In this prototype, each power-up has 99 uses per level.

| Power-Up | What it does |
| :--- | :--- |
| ➕ **Add Stack** | Adds 1 extra space on the conveyor for this level. |
| 🔀 **Shuffle** | Shuffles the food stacks in the queue. |
| ✋ **Hand** | Lets you pick a stack from **deeper in the queue** and send it straight to the belt. |
| 🎨 **Clear Color** | Pick a food type. All customers who want it are served at once, and its stacks are removed. |

---

## 🔧 Built With

- **Unity 6.3 LTS** (URP)
- **C#**
- **DOTween** for animation
- **Dreamteck Splines** for the conveyor path
- **Unity Input System** for tap input
- **ScriptableObjects** for levels and food data
- **Platform: Android only** (portrait)

---

## 🚀 Status & Future Roadmap

**Where we stopped:** The core game works. It has the queue, conveyor, rack, customers, 4 power-ups, timed customers, a tutorial, win and lose screens, and 30 levels.

**What can be improved:**

- **UI and art:** The menus, buttons, and screens. A full art and UI polish pass would make a big difference.
- **More content:** More food types, more characters, more levels, and more environments.
- **Table groups:** The design document planned groups of customers who sit at a table and leave together. We did not build this yet.
- **Level balance:** Test all 30 levels with real players, or build a solver tool to check that every level can be won and the difficulty feels fair.
- **Power-up design:** Add a simple way to earn power-ups and introduce each one slowly with a tutorial.
- **Game feel:** More effects and more reactions from customers.
- **Performance:** Test on more Android devices, especially with large crowds like Level 28.
- **More platforms:** iOS support.

**This was a fast and fun project. We are happy with how much we built in such a short time. 🍽️**
