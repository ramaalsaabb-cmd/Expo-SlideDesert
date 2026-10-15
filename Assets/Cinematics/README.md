# Desert character cinematics

Open DesertOverview after Unity imports the scripts and animation FBXs. Run **Tools > Desert VR > Build Editable Character Cinematics** manually. The builder does not run automatically during reload or scene opening.

The builder creates `Mufleh Selected.playable` and `Saqeera Selected.playable`, their editable `.anim` position/rotation/camera curves, and two bound PlayableDirectors on separate **Mufleh Timeline** and **Saqeera Timeline** children under **Character Cinematics** in DesertOverview. Select either director and open Window > Sequencing > Timeline to edit it.

The Desert Robe character is Mufleh, Traditional Dress is Saqeera, and White Thobe is the companion by the car. Walking and sitting use the supplied humanoid FBX animations. Both walking clips start at Timeline time 1.000 s. Four other characters are seated throughout. A Timeline Animation Track animates the existing XR Origin camera rig; HMD-local tracking remains available. Cinemachine is not installed, so it is not added.

The first-scene menu saves its selection in `Desert.SelectedCharacter`. On loading the overview, supporting code chooses one Director; Timeline controls all character and camera movement. Direct overview testing defaults to Mufleh if no selection was saved.

Car, sled and tent positions, rotations and scales are checked before the scene is saved. Automatic layout scripts are disabled. These environment objects are never animated or repositioned by the cinematic builder.

Read `BuildReport.txt` to confirm generation completed, or to identify an import/rig issue. No COMPLETE report means setup has not finished. Validate animation retargeting, seated heights, garment clipping and VR comfort in Unity before deployment. The supplied sitting pose is blended from walking; no separate sit-down transition animation was supplied.

The builder preserves existing authored Timeline assets. To rebuild deliberately, first move the generated .playable and .anim assets out of this folder, then use the Tools menu again. Do not delete your edited timelines.

For an existing cinematic, **Tools > Desert VR > Update Selected Camera and White Thobe Role** updates the roles and follow camera tracks without rebuilding the Timelines or changing the vehicle/tent layout. The update also runs once after script compilation when DesertOverview is loaded.
