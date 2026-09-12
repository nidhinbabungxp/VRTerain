# Car mirror displays — Unity HDRP

Made from the glass surfaces in your uploaded `CarModelVR_Final.glb`.

## Quick setup: automatically fit all three mirrors

1. Extract this ZIP.
2. In Unity, open **Assets > Import Package > Custom Package**, select `Car_Mirror_Displays.unitypackage`, and import all items. Wait for script compilation.
3. Exit Play mode. In the Hierarchy, select your complete **CarModelVR** instance, containing both doors and the centre mirror.
4. Choose **Tools > Car Mirrors > Add Exact Fit Displays**.
5. Three new display objects are attached to the original glass meshes. They inherit the mirror and door movement. No manual position, rotation or scale adjustment is needed for the supplied source model.
6. Disable your old test Cube and Quad objects. Keep the original mirror objects and housings enabled.
7. Select each new display. Its material is under **Mesh Renderer > Materials**. Assign that mirror's **Render Texture** to the HDRP/Unlit **Color texture** slot. Keep the colour white. Separate materials are created for all three displays.
8. On each mirror camera, exclude **MirrorSurface** from **Culling Mask**. Keep that layer visible on the driver camera. If the original glass is also visible in the camera feed, exclude its layer too; note that a layer applies to an entire Renderer, including its housing submesh.
9. Save the scene.

The installer connects a Render Texture automatically when it finds exactly one asset with the appropriate name:

| Display object | Original mesh | Render Texture name | Suggested starting resolution |
| --- | --- | --- | --- |
| LeftMirror_Display | Mirror_L | RT_LeftMirror | 700 × 512 |
| RightMirror_Display | Mirror_R | RT_RightMirror | 700 × 512 |
| CentreMirror_Display | Center Mirror / Center_Mirror | RT_CenterMirror or RT_CentreMirror | 1024 × 256 |

If a Render Texture is missing or ambiguous, the new material appears white until you assign one. Cameras and camera aim are not created by this package. Retain your existing left-mirror camera, and use separate cameras and textures for the other two mirrors.

The new meshes have full 0–1 UV mapping. They do not have a horizontal mirror flip baked in. If the view needs a mirror-style horizontal flip, set the material's Color texture **Tiling X = -1** and **Offset X = 1**. Leave Y tiling at 1 and Y offset at 0. Check with a recognisable object beside the car before keeping the flip.

To remove the new displays, delete only the three `...Mirror_Display` child objects. The installer does not alter the original glass geometry or existing materials. It skips an existing display of the same name when run again.

## Rectangular quads

For literal four-vertex rectangular surfaces, use **Tools > Car Mirrors > Add Rectangular Quads** instead. Each rectangle matches its glass surface's projected width and height and is placed in its mirror plane. A rectangle reaches outside a rounded glass outline at its corners; the exact-fit display is the better match for your existing frames.

Use one variant at a time. If you switch variants, disable or remove the other set first to avoid overlapping displays.

## GLB models included

- `Models/Car_Mirrors_ExactFit.glb` — three separate fitted glass display meshes, with their original rounded outlines and corrected UVs.
- `Models/Car_Mirrors_Rectangular_Quads.glb` — three separate rectangular quads with matching projected dimensions.

These GLBs contain the mirror surfaces only. Import with the same GLB importer used for the car. To place the combined model manually, parent it beneath the car's original **Car_P** coordinate root (or the corresponding CarModelVR root if Car_P was renamed). Set local position and rotation to zero and local scale to one. It preserves the source ancestor transforms for the closed-door position.

For doors that animate, reparent each imported display beneath the corresponding original mirror mesh using Unity's keep-world-position behaviour. The automatic installer handles this attachment directly and is preferable.

When replacing an imported GLB material, use **HDRP/Unlit** and assign the Render Texture to **Color**. Importers can handle texture V orientation differently; if a manually imported model shows an upside-down camera texture, use **Tiling Y = -1, Offset Y = 1**. The native installer generates Unity UVs directly and avoids that importer difference.

## Measurements

Dimensions are measured in the glass plane at the original model scale. Scene parent scaling changes the final world dimensions.

| Mirror | Width | Height | Exact-fit vertices | Exact-fit triangles |
| --- | ---: | ---: | ---: | ---: |
| Left | 175.2 mm | 128.0 mm | 48 | 46 |
| Right | 175.2 mm | 128.0 mm | 48 | 46 |
| Centre | 245.5 mm | 61.2 mm | 28 | 26 |

Fitted surfaces are offset **0.7 mm toward the viewer** from the original glass. They preserve the original contours, triangulation and slight centre-mirror curvature. Quads use the bounding rectangle and a small outward clearance.

The side-mirror source UVs had V = 0 throughout, collapsing the camera view into a line. The new UVs cover the full texture in both directions. The centre mirror receives consistent mapping too.

## Compatibility and checks

Target: Unity 6 with HDRP 17, matching your screenshots. The installer is editor-only; generated displays use standard MeshFilter and MeshRenderer components and add no scripts or colliders to the car. It creates native mesh and material assets under `Assets/CarMirrorDisplays/Generated` and reuses or creates the `MirrorSurface` layer. Mesh bounds are checked before installation to avoid fitting to a different model.

The GLB geometry, indices, normals, UV coverage, source transforms and exact-fit surface offsets were validated. The preview is a geometry/UV inspection, not a Unity screenshot. Unity Editor was unavailable during creation, so the installer has not been executed in Unity. The GLB files provide an independent manual import route.

Unity documentation used for the installer:
- [Mesh UV data](https://docs.unity3d.com/ScriptReference/Mesh.SetUVs.html)
- [HDRP Unlit materials](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/unlit-material.html)
- [HDRP material validation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/api/UnityEngine.Rendering.HighDefinition.HDMaterial.html)
