# Sled asset

Reference-inspired tan polymer sled with open side grips, geometric turquoise/lime graphics, traction ribs, runners and yellow tow rope. Original simplified graphics; reference logos are not reproduced. Assumed dimensions: 1.20 m long and approximately 0.45 m wide. Ground-level central pivot; Unity Y-up, +Z forward.

Files: Sled.blend (editable scene and studio setup), Sled_Unity.fbx (game mesh only), Sled.glb (portable materials), Sled_Collision.fbx (four optional collision proxy boxes), Sled_Preview.png, SledPrefabBuilder.cs.

## Unity setup

1. Copy Sled_Unity.fbx into your project's Assets folder. Enable Convert Units and verify the body measures 1.20 Unity units. Keep instance scale at 1. Import normals and generate lightmap UVs only if using baked lighting.
2. Extract/remap the five imported materials to your rendering pipeline's Lit shader. Graphics use geometry and flat colors, so no external textures are required. Meshes have UVs for later texture work.
3. Put SledPrefabBuilder.cs in Assets/Editor. Select the FBX in the Project window and choose Tools > Sled > Create Prefab. The builder creates four box colliders, a 2.5 kg Rigidbody (a gameplay starting value), and a HandleAttach transform. The separate collision FBX is an alternative reference, not required by the builder.
4. For grabbing, add your installed XR Interaction Toolkit's XR Grab Interactable to the prefab root; use HandleAttach as the attach transform. Configure its collider list and interaction layers for your XR rig. The rope is a static mesh, and the handle moves with the sled. Rope simulation, riding locomotion, and steering are not included.
5. Test scale, grab alignment, ground contact and frame time on your headset. The rail colliders deliberately span the grip openings; use smaller compound boxes if hand physics must pass through the holes.

The asset is 17,306 triangles, six mesh renderers and five materials. No transparency or texture bandwidth is required. This is a modest single-asset budget, not a headset performance guarantee. No LOD variants are included.

The Blender scene and FBX/GLB exports were generated successfully and the render was visually reviewed. The Unity builder has not been compiled or run in a Unity project; integration and on-device VR validation remain project-specific.

Documentation: https://docs.unity.com/en-us/engine/6000.6/manual/assets-and-media/asset-types/models/importing/fbximporter-model
XR examples: https://github.com/Unity-Technologies/XR-Interaction-Toolkit-Examples/blob/main/Documentation/GrabInteractables.md

