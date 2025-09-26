# **Fungi vs. Bacteria - Game Development Guidelines (MVP)**

## **1. Core Concept & Story**

- **Main Storyline:** Players control a network of defensive fungi to protect environments from bacterial invasions
- **Player's Goal:** Stop waves of bacteria from reaching critical points in the environment using strategic fungal tower placement
- **Setting:** Microscopic battlefield in various biomes (MVP: Dark Rainforest environment)

---

## **2. MVP Features**

### **2.1 Core Gameplay**

- **Path System:**

  - Fixed paths for bacteria to follow - Environments will have different levels - predefined paths (as they will rotate in the different environments)
  - Clear visual indicators for bacterial movement routes
  - Single entry point and exit point per level

- **Resource System:**

  - Starting currency (mycelium) for tower placement
  - Auto reward mycelium from defeated bacteria
  - Simple resource collection system

- **Tower System (4 Basic Types for MVP):**

  1. Mycelial Network (Basic Tower)

     - Low cost, basic damage
     - Basic attack animation

  2. Penicillium (Damage Tower)

     - Medium cost, high single-target damage
     - Longer range
     - Projectile-based attack

  3. Amanita (Area Effect Tower)

     - High cost, area damage
     - Poison cloud effect
     - Status effect visualization

  4. Trichoderma (Support Tower)
     - Low cost, low damage
     - Slowing cloud effect on bacteria
     - Basic attack animation

### **2.2 Enemy System (MVP)**

- **3 Basic Enemy Types:**

  1. Basic Bacteria (Cocci)

     - Normal speed
     - Normal health, normal numbers

  2. Fast Bacteria (Spirillum)

     - Fast speed
     - Low health, high numbers

  3. Armored Bacteria (Bacilli)

     - Slower but tougher
     - Resistant to single-target damage

  4. Boss Bacteria (Basic Boss)
     - Appears at end of waves
     - High health pool
     - Basic attack pattern

### **2.3 Essential UI Elements**

- Health bar
- Resource/mycelium counter
- Wave information

- Tower placement interface
- Tower range indicators

- Enemy health bars

- Main menu
- Environment selection screen
- Level selection screen
- Pause screen
- Victory/defeat screens
- Settings menu

### **2.4 Core Progression**

- 5 levels for MVP
- Linear progression
- Basic difficulty scaling
- Simple upgrade system for towers (damage, range, speed)

---

## **3. Technical Guidelines**

### **3.1 Performance Targets**

- 60 FPS on mid-range devices
- Maximum 50 active enemies
- Optimize particle effects and animations
- Efficient tower targeting system

### **3.2 Code Structure**

- Modular tower system using ScriptableObjects
- Enemy pooling system for performance
- Event-based communication between systems
- Clear separation between UI and game logic

### **3.3 Art Style (MVP)**

- Simple but distinctive microscopic aesthetic
- Clear visual hierarchy
- Color-coding for different tower/enemy types
- Basic particle effects for impacts
- Minimal but functional animations

### **3.4 Sound Design (MVP)**

- Basic sound effects for:
  - Tower placement
  - Tower attacks
  - Enemy death
  - Resource collection
- One background music track
- UI feedback sounds

---

## **4. Post-MVP Features**

### **4.1 Gameplay Extensions**

- Additional tower types (Cordyceps, Trichoderma)
- Complex upgrade paths for each tower
- Environmental hazards
- Dynamic path generation
- Multiple entry/exit points
- Tower synergy systems

### **4.2 Content Expansion**

- Additional environments/biomes
- More enemy types
- Boss variants
- Challenge modes
- Endless mode

### **4.3 Systems**

- Research system
- Advanced progression system
- Achievement system
- Daily challenges
- Leaderboards

### **4.4 Technical Improvements**

- Advanced particle systems
- Complex animations
- Environmental effects
- Dynamic difficulty adjustment
- Save/load system
- Cloud saves

### **4.5 Monetization**

- Premium towers
- Cosmetic upgrades
- Battle pass system
- Ad-based rewards

---

## **5. Development Priorities**

1. Core tower placement and targeting system
2. Basic enemy movement and waves
3. Resource system
4. Essential UI elements
5. First three tower types
6. Basic progression system
7. Tutorial system
8. Core sound effects
9. Basic visual effects
10. Level design (5 initial levels)

---

## **6. Quality Assurance**

### **MVP Testing Focus**

- Tower placement accuracy
- Enemy pathfinding
- Resource balance
- Level difficulty progression
- Performance optimization
- Basic tutorial effectiveness
- UI clarity and responsiveness

### **Critical Bugs**

- Tower targeting issues
- Resource calculation errors
- Wave spawning problems
- Game-breaking progression bugs
