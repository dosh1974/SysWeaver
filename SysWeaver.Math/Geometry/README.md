# Geometry

[⬆ SysWeaver.Math](../README.md)

> Spatial search and earth geometry: an N-dimensional KD-tree with its supporting types, and spherical earth calculations.

| | |
|---|---|
| **Project** | [SysWeaver.Math](../README.md) |
| **Kind** | Source code (namespace `SysWeaver.Geometry`) |

## Purpose

`KDTree<TDimension, TNode>` stores points (with an associated node object each) in an array-backed binary tree and answers nearest-neighbour queries. The other KD-tree files are its building blocks: the hyper-rectangle used to prune the search, the bounded priority list that collects the nearest results, and the index arithmetic / navigator for the array-backed tree. `Earth` converts between latitude / longitude and cartesian coordinates and computes great-circle distances, e.g. to build a KD-tree of locations.

## Contents

| Item | Description |
|---|---|
| [`KDTree.cs`](KDTree.cs) | N-dimensional KD-tree with nearest-neighbour search |
| [`HyperRect.cs`](HyperRect.cs) | N-dimensional rectangle (`HyperRect<T>`) |
| [`BoundedPriorityList.cs`](BoundedPriorityList.cs) | A list of limited length kept sorted by priority |
| [`BoundedPriorityListExtensions.cs`](BoundedPriorityListExtensions.cs) | Converts a priority list of KD-tree indices into points and nodes |
| [`BinaryTreeNavigation.cs`](BinaryTreeNavigation.cs) | Index arithmetic for a binary tree stored in an array (left, right, parent) |
| [`BinaryTreeNavigator.cs`](BinaryTreeNavigator.cs) | Navigates such a tree with left / right / parent properties |
| [`Earth.cs`](Earth.cs) | Earth constants, latitude / longitude to cartesian and back, angular differences, `DistanceKm` |

## Relationships

- **Part of:** [SysWeaver.Math](../README.md)
- **Related:** [Search/](../Search/README.md)
