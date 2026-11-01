# AI Content Pipeline (Unity Editor Tool)

[![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

> **A professional, modular Unity Editor tool that automates RPG item creation and icon generation by combining fast LLM structured text generation (Groq API) with high-performance local/cloud image rendering (ComfyUI / Flux).**

---

## 🏗️ Architecture & Data Flow

The tool follows a clean **Strategy Pattern** and decouples data generation from asset creation, ensuring zero UI freezes (`async/await` implementation) and easy extensibility for new AI backends.

```mermaid
graph TD
    A[User Input / Batch Prompts] --> B[AiGeneratorWindow (Editor UI)]
    B --> C{Select Backend}
    
    C -->|Groq API| D[GroqTextProvider (JSON Mode)]
    D -->|Structured JSON| E[ItemGenerationDto]
    
    E --> F{Image Provider}
    F -->|Local| G[ComfyUIImageProvider (ComfyUI API)]
    F -->|Cloud| H[PollinationsAPI (Flux Model)]
    F -->|Fallback| I[Procedural Rarity Generator]
    
    E & G --> J[ItemAssetFactory]
    E & H --> J
    E & I --> J
    
    J --> K[AssetDatabase.CreateAsset]
    K --> L[.asset ScriptableObject + Sprite Icon]