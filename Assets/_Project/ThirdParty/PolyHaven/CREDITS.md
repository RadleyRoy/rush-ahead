# Poly Haven assets

Everything in this folder comes from [Poly Haven](https://polyhaven.com) and is released under
[CC0](https://creativecommons.org/publicdomain/zero/1.0/): free for any use, no credit required. We credit them anyway.

- Sky: kloofendal_48d_partly_cloudy_puresky (HDRI, 2K)
- Textures (1K): cliff_side, aerial_grass_rock, bark_brown_02, weathered_planks, rusty_metal_02, forest_leaves_02, coast_sand_01
- Models (1K): rock_07, rock_09, rock_face_01, ocean_buoy, wooden_barrels_01, wooden_crate_02, fern_02
- Ground textures (1K, Phase 15): forrest_ground_01, brown_mud_leaves_01, rocky_terrain_02, mossy_rock, rock_face_03, brown_mud_02
- Plants and rocks (1K models, Phase 15): shrub_02, shrub_04, weed_plant_02, nettle_plant, rock_moss_set_01, rock_moss_set_02, boulder_01,
  namaqualand_cliff_01
- Needle textures only (1K, Phase 15): pine_sapling_small (twig), fir_sapling (twigs)

`Editor/PolyHavenArt.cs` sets their import options and makes the materials from them (`Art/PolyHaven`); `Editor/NatureArt.cs`
cuts the Phase 15 scans down to phone size with Unity's mesh LOD tools, makes the terrain layers, and stamps leaf sprays for
the trees from shrub_02's leaves (`Art/PolyHaven/Meshes`, `Generated`, `Terrain`).
