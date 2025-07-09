# ChiseMF - Mesh Delete by Mask

[![Unity Version](https://img.shields.io/badge/Unity-2019.4%2B-blue.svg)](https://unity3d.com/get-unity/download)
[![NDMF](https://img.shields.io/badge/NDMF-1.6.0%2B-green.svg)](https://github.com/bdunderscore/ndmf)
[![Version](https://img.shields.io/badge/Version-1.0.0-orange.svg)]()

> マスクテクスチャでメッシュを削除するVRChatアバター用ツール

## 特徴
- アバター内のメッシュオブジェクトを自動検出
- 複数マテリアルの個別設定に対応
- 黒/白削除の2つのモード
- NDMF統合による安全な非破壊処理

## インストール

**前提条件**: Unity 2019.4+, VRChat SDK3, NDMF 1.6.0+

**VPM**：[https://chisenon.github.io/chisenote_vpm/](https://chisenon.github.io/chisenote_vpm/)

## 使い方

1. `GameObject → Create Empty → Add Component → ChiseNote/Chise Remove Mesh By Mask`
2. **Target Mesh Object**でメッシュを選択
3. **Mask**テクスチャを設定
4. **Mode**を選択 (`Remove Black` / `Remove White`)

| 設定項目 | 説明 |
|---------|------|
| Target Mesh Object | 処理対象のメッシュ |
| Mask | マスクテクスチャ |
| Mode | 削除モード（黒/白） |

## 注意事項
- マスクテクスチャは**ReadWrite有効**が必要
- 推奨解像度: 512x512以下

## 謝辞
このプロジェクトは [Avatar Optimizer](https://github.com/anatawa12/AvatarOptimizer) のコードを参考にしています。
