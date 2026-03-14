<p align="center">
  <img src="https://github.com/user-attachments/assets/2ed71f8c-9a60-41d4-bf2a-79ba50ae38d8" width="400" alt="RoseMotion UI Preview" />
</p>




##  Features / 功能特性

#### **Core Performance / 核心性能**
*   **High-Frequency T-Code Output**: High-speed Serial (COM) output with customizable transmission frequency up to 200Hz.
*   **高频 T-Code 输出**：支持物理串口 (COM) 通信，可自定义数据传输频率最高可达 200Hz。

#### **Unity Integration / 引擎集成**
*   **Deep Integration**: Uses Reflection to sniff `CubismParameter` in real-time within the Live2D SDK.
*   **深度 Unity 集成**：基于 反射技术 实时嗅探 Live2D SDK 中的 `CubismParameter` 核心组件。

#### **Motion Mapping / 动作映射**
*   **Live2D → T-Code**: Map any character parameters (Breathing, Body Sway, Eye movement) to hardware axes.
*   **Dynamic Mapping**: Full mapping for **SR6** and backward compatible with **OSR2**.
*   **动态动作映射**：支持将任意角色参数（呼吸、身体摇摆、眼神转向等）映射至硬件轴向。完整支持 **SR6** 六轴映射，并向下兼容 **OSR2**。

#### **Data Processing / 数据处理**
*   **Real-time Processing**: Precise control via custom **Multiplier** and **Offset**.
*   **Global Smoothing**: Built-in Lerp algorithm to filter out jittery animations and protect hardware.
*   **实时数据处理**：通过自定义倍率与偏移实现精准行程控制。内置Lerp 平滑算法，有效过滤动画抽搐，保护硬件电机。

<sub><font color="#888">*A comfortable UI / 舒适的 UI*</font></sub>


---

> **Note:** Currently only supports Mono-backend games developed with the standard Cubism SDK.
> 
> **注：** 目前仅支持基于Mono后端且由标准 Cubism SDK 开发的游戏。


<p align="right">
  <font color="#999">
    <sub>⚠️本项目为开源项目，若你通过付款获取或间接获取，则是被征收了智商税。</sub>
  </font>
</p>
Live2D-to-SR6-Motion
