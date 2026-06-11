# SBOLK – Top-Down Arena Shooter Game

## 1. Application Overview

**SBOLK** is a 2D top-down arena shooter game developed using **Godot 4.4**. Players fight increasingly difficult waves of enemies, earn coins, purchase upgrades, unlock cosmetic items, and compete on a global leaderboard powered by Firebase.

### Purpose

The project aims to provide an engaging action-survival gaming experience while demonstrating game development concepts such as:

- Enemy AI behavior
- Wave-based progression
- Weapon systems
- Online authentication
- Cloud database integration
- Leaderboards and player data storage

### Objectives

- Develop a functional arena shooter game.
- Implement multiple weapon types.
- Create a scalable wave progression system.
- Integrate cloud-based user authentication.
- Store player scores and cosmetic data online.

### Target Users

- Casual gamers
- Students interested in game development
- Players who enjoy wave-survival and shooter games

---

## 2. System Screenshots

### Authentication Screen

The Authentication Screen serves as the user login and registration interface for SBOLK. It allows players to securely access their accounts using Firebase Authentication before entering the game.

- **Email Field** – Allows users to enter their registered email address.
- **Password Field** – Allows users to enter their account password securely.
- **Log In Button** – Authenticates existing users and grants access to the game's main menu.
- **Sign In Button** – Creates a new user account for first-time players.
- **Error Message Display** – Provides feedback when authentication fails, helping users identify and correct login issues.

---

### Main Menu Screen

The Main Menu Screen serves as the central navigation hub of the SBOLK game. It is the first interface displayed after a player successfully logs in. The screen features a vibrant and colorful retro-inspired design with animated visual effects that match the game's arcade aesthetic.

Players can access the following functions:

- **Play** – Starts a new game session and enters the arena.
- **Tutorial** – Displays gameplay instructions, controls, and basic mechanics to help new players learn the game.
- **Sign Out** – Logs the current user out of their account and returns them to the authentication screen.
- **Shop** – Allows players to purchase upgrades, items, and other in-game enhancements using earned currency.
- **Trade** – Provides access to item trading features between players.
- **Inventory** – Displays the player's collected items, cosmetics, and equipped equipment.
- **Leaderboard** – Shows the highest scores achieved by players globally through Firebase integration.

---

### Leaderboard Screen

The Leaderboard Screen displays the highest scores achieved by players in SBOLK, allowing users to compare their performance with others worldwide. The leaderboard is connected to Firebase, ensuring that player rankings are updated and stored in real time.

- **Player Rankings** – Displays players in descending order based on their highest scores.
- **Player Identification** – Shows the registered account associated with each score.
- **Score Records** – Displays the highest score achieved by each player.

---

### Inventory Screen

The Inventory Screen allows players to view, manage, and customize the cosmetic items they have collected throughout the game. It serves as the player's personal collection menu, where owned cosmetics can be equipped or unequipped to customize the character's appearance.

- **Owned Cosmetic Items** – Displays all hats and cosmetic accessories unlocked or purchased by the player.
- **Item Information** – Shows the name and quantity of each owned cosmetic item.
- **Equip Button** – Allows players to select and equip a cosmetic item to their character.
- **Unequip Button** – Removes the currently equipped cosmetic item.
- **Equipped Item Indicator** – Displays the cosmetic item that is currently equipped by the player.
- **Close Button** – Exits the inventory screen and returns the player to the previous menu.

---

### Trade Screen (Marketplace)

The Trade Screen enables players to buy, sell, and trade cosmetic items with other players through an online player-driven economy. This feature allows users to exchange collectible hats and other cosmetic items using in-game currency.

- **Item Listings** – Displays cosmetic items currently available for purchase from other players.
- **Seller Information** – Shows the player who posted the item for sale.
- **Price Display** – Indicates the selling price and quantity of each listed item.
- **Buy Button** – Allows players to purchase listed items using in-game currency.
- **Post Item Feature** – Enables players to list their own items on the marketplace for sale.
- **History Tab** – Displays previous transactions, purchases, and sales made by the player.
- **Refresh Button** – Updates the marketplace listings with the latest available items.
- **Listing Duration** – Shows the remaining time before an item listing expires.
- **Close Button** – Returns the player to the previous menu.

---

### Cosmetic Shop Screen

The Cosmetic Shop Screen allows players to purchase collectible cosmetic items using in-game currency earned through gameplay. The shop provides a rotating selection of hats and accessories that players can acquire to customize their character's appearance.

- **Item Catalog** – Displays cosmetic items currently available for purchase.
- **Item Information** – Shows the item's name, rarity, price, and ownership status.
- **Buy Button** – Allows players to purchase an item if they have sufficient in-game currency.
- **Currency Display** – Shows the player's current amount of available coins.
- **Ownership Counter** – Indicates how many copies of a specific cosmetic item the player owns.
- **Shop Refresh Timer** – Displays the remaining time before the shop inventory automatically refreshes with new items.

---

### SBOLK Analytics – OLAP Dashboard

The Trade Summary Panel provides a quick and convenient overview of the most important marketplace metrics, displaying key KPIs such as Total Trades, Total Volume, Average Price, and Top Item, all of which automatically update and recalculate in real time whenever the user adjusts the active filters, ensuring the data shown is always relevant to the current selection.

A real-time marketplace analytics dashboard built with a retro terminal aesthetic (green-on-black), designed to monitor and analyze virtual economy trade data through an OLAP (Online Analytical Processing) pipeline.

### Key Features

- Five enemy types
- Wave progression system
- Between-wave upgrade shop
- Cosmetic hat system
- Global leaderboard
- Firebase authentication
- Save and load player data
- Visual shader effects

---

## 3. Technologies and Tools Used

### Programming Languages

- C#
- GDScript

### Game Engine

- Godot 4.4

### Frameworks and Libraries

- Godot Firebase Addon
- Trail2D Plugin
- BBCode Editor Plugin

### Database and APIs

- Firebase Authentication
- Firebase Realtime Database

### Software Tools

- Godot Engine
- Git
- GitHub

---

## 4. Algorithms and Methods

### Enemy AI

Different enemy behaviors include:

1. **Basic Enemy** – Directly chases the player.
2. **Dash Enemy** – Charges and rapidly dashes toward the player.
3. **Teleport Enemy** – Teleports behind the player periodically.
4. **Gun Enemy** – Maintains distance and shoots projectiles.
5. **Bomb Enemy** – Rushes toward the player and explodes.

### Wave Progression

- Enemy difficulty increases after each completed wave.
- Enemy health and damage scale dynamically.

### Leaderboard System

- Compares player scores.
- Updates Firebase only if the new score exceeds the previous best score.

---

## 5. UI/UX Design Documentation

### Design Software Used

- Godot Editor
- Aseprite (for pixel-art assets)

### User Interface Components

**Title Screen** – Provides access to:
1. Start Game
2. Login
3. Leaderboard

**Gameplay HUD** – Displays:
- Health
- Current Weapon
- Gold
- Money

**Shop Interface** – Allows players to:
- Spend gold
- Purchase upgrades
- Improve performance

**Cosmetic Shop** – Features:
- 12 unlockable hats
- Rotating hourly offers
- Equipment management

### Visual Design

- Pixel-art style graphics
- Retro-inspired font (Monogram)
- Shader effects: Swirl, Glitch, Background animation

### Additional Features

- Firebase Authentication
- Global Leaderboard
- Cosmetic Inventory System
- Coin Economy
- Portal-Based Progression
- Healing Pot System
- Weapon Switching
- Dynamic Difficulty Scaling
- Shader-Based Visual Effects

---

## 6. Collaboration and Contribution Report

| Name | Role |
|---|---|
| Sigmon Earl | Project Leader / Lead Programmer |
| Enzo Rebancos | Assistant Project Leader |
| Magdaraog Mabey | UI/UX Designer & Asset Designer |
| Iverson Klerk T. Perno | Game Tester & Documentation |
