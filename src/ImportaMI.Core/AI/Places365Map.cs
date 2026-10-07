using System;
using System.Collections.Generic;

namespace ImportaMI.Core.AI;

/// <summary>
/// Mappa le 365 classi di Places365 alle categorie fotografiche di ImportaMI.
/// Ogni indice di Places365 può corrispondere a una o più PhotoCategory, con pesi diversi.
/// Le classi non mappate vengono ignorate (non tutte le scene sono rilevanti per la fotografia amatoriale).
/// </summary>
internal static class Places365Map
{
    /// <summary>
    /// Etichette di Places365 in ordine di indice (0..364).
    /// Derivate da https://github.com/CSAILVision/places365/blob/master/categories_places365.txt
    /// </summary>
    internal static readonly string[] Labels =
    [
        "airfield",                    // 0
        "airplane_cabin",              // 1
        "airport_terminal",            // 2
        "alcove",                      // 3
        "alley",                       // 4
        "amphitheater",                // 5
        "amusement_arcade",            // 6
        "amusement_park",              // 7
        "apartment_building_outdoor",  // 8
        "aquarium",                    // 9
        "aqueduct",                    // 10
        "arcade",                      // 11
        "arch",                        // 12
        "archaelogical_excavation",    // 13
        "archive",                     // 14
        "arena_hockey",                // 15
        "arena_performance",           // 16
        "arena_rodeo",                 // 17
        "army_base",                   // 18
        "art_gallery",                 // 19
        "art_school",                  // 20
        "art_studio",                  // 21
        "artists_loft",                // 22
        "assembly_line",               // 23
        "athletic_field_outdoor",      // 24
        "atrium_public",               // 25
        "attic",                       // 26
        "auditorium",                  // 27
        "auto_factory",                // 28
        "auto_showroom",               // 29
        "badlands",                    // 30
        "bakery_shop",                 // 31
        "balcony_exterior",            // 32
        "balcony_interior",            // 33
        "ball_pit",                    // 34
        "ballroom",                    // 35
        "bamboo_forest",               // 36
        "bank_vault",                  // 37
        "banquet_hall",                // 38
        "bar",                         // 39
        "barn",                        // 40
        "barndoor",                    // 41
        "baseball_field",              // 42
        "basement",                    // 43
        "basketball_court_indoor",     // 44
        "bathroom",                    // 45
        "bazaar_indoor",               // 46
        "bazaar_outdoor",              // 47
        "beach",                       // 48
        "beach_house",                 // 49
        "beauty_salon",                // 50
        "bedchamber",                  // 51
        "bedroom",                     // 52
        "beer_garden",                 // 53
        "beer_hall",                   // 54
        "berth",                       // 55
        "biology_laboratory",          // 56
        "boardwalk",                   // 57
        "boat_deck",                   // 58
        "boathouse",                   // 59
        "bookstore",                   // 60
        "booth_indoor",                // 61
        "botanical_garden",            // 62
        "bow_window_indoor",           // 63
        "bowling_alley",               // 64
        "boxing_ring",                 // 65
        "bridge",                      // 66
        "building_facade",             // 67
        "bullring",                    // 68
        "burial_chamber",              // 69
        "bus_interior",                // 70
        "bus_station_indoor",          // 71
        "butchers_shop",               // 72
        "butte",                       // 73
        "cabin_outdoor",               // 74
        "cafeteria",                   // 75
        "campsite",                    // 76
        "campus",                      // 77
        "canal_natural",               // 78
        "canal_urban",                 // 79
        "candy_store",                 // 80
        "canyon",                      // 81
        "car_interior",                // 82
        "carrousel",                   // 83
        "castle",                      // 84
        "catacomb",                    // 85
        "cemetery",                    // 86
        "chalet",                      // 87
        "chemistry_lab",               // 88
        "childs_room",                 // 89
        "church_indoor",               // 90
        "church_outdoor",              // 91
        "classroom",                   // 92
        "clean_room",                  // 93
        "cliff",                       // 94
        "closet",                      // 95
        "clothing_store",              // 96
        "coast",                       // 97
        "cockpit",                     // 98
        "coffee_shop",                 // 99
        "computer_room",               // 100
        "conference_center",           // 101
        "conference_room",             // 102
        "construction_site",           // 103
        "corn_field",                  // 104
        "corral",                      // 105
        "corridor",                    // 106
        "cottage",                     // 107
        "courthouse",                  // 108
        "courtyard",                   // 109
        "creek",                       // 110
        "crevasse",                    // 111
        "crosswalk",                   // 112
        "dam",                         // 113
        "delicatessen",                // 114
        "department_store",            // 115
        "desert_sand",                 // 116
        "desert_vegetation",           // 117
        "desert_road",                 // 118
        "diner_outdoor",               // 119
        "dining_hall",                 // 120
        "dining_room",                 // 121
        "discotheque",                 // 122
        "doorway_outdoor",             // 123
        "dorm_room",                   // 124
        "downtown",                    // 125
        "dressing_room",               // 126
        "driveway",                    // 127
        "drugstore",                   // 128
        "elevator_door",               // 129
        "elevator_lobby",              // 130
        "elevator_shaft",              // 131
        "embassy",                     // 132
        "engine_room",                 // 133
        "entrance_hall",               // 134
        "escalator_indoor",            // 135
        "excavation",                  // 136
        "fabric_store",                // 137
        "farm",                        // 138
        "fastfood_restaurant",         // 139
        "field_cultivated",            // 140
        "field_wild",                  // 141
        "field_road",                  // 142
        "fire_escape",                 // 143
        "fire_station",                // 144
        "fishpond",                    // 145
        "flea_market_indoor",          // 146
        "florist_shop_indoor",         // 147
        "food_court",                  // 148
        "football_field",              // 149
        "forest_broadleaf",            // 150
        "forest_path",                 // 151
        "forest_road",                 // 152
        "formal_garden",               // 153
        "fountain",                    // 154
        "galley",                      // 155
        "garage_indoor",               // 156
        "garage_outdoor",              // 157
        "gas_station",                 // 158
        "gazebo_exterior",             // 159
        "general_store_indoor",        // 160
        "general_store_outdoor",       // 161
        "gift_shop",                   // 162
        "glacier",                     // 163
        "golf_course",                 // 164
        "greenhouse_indoor",           // 165
        "greenhouse_outdoor",          // 166
        "grotto",                      // 167
        "gymnasium_indoor",            // 168
        "hangar_indoor",               // 169
        "hangar_outdoor",              // 170
        "harbor",                      // 171
        "hardware_store",              // 172
        "hayfield",                    // 173
        "heliport",                    // 174
        "highway",                     // 175
        "home_office",                 // 176
        "home_theater",                // 177
        "hospital",                    // 178
        "hospital_room",               // 179
        "hot_spring",                  // 180
        "hotel_outdoor",               // 181
        "hotel_room",                  // 182
        "house",                       // 183
        "hunting_lodge_outdoor",       // 184
        "ice_cream_parlor",            // 185
        "ice_floe",                    // 186
        "ice_shelf",                   // 187
        "ice_skating_rink_indoor",     // 188
        "ice_skating_rink_outdoor",    // 189
        "iceberg",                     // 190
        "igloo",                       // 191
        "industrial_area",             // 192
        "inn_outdoor",                 // 193
        "islet",                       // 194
        "jacuzzi_indoor",              // 195
        "jail_cell",                   // 196
        "japanese_garden",             // 197
        "jewelry_shop",                // 198
        "junkyard",                    // 199
        "kasbah",                      // 200
        "kennel_outdoor",              // 201
        "kindergarden_classroom",      // 202
        "kitchen",                     // 203
        "lagoon",                      // 204
        "lake_natural",                // 205
        "landfill",                    // 206
        "landing_deck",                // 207
        "laundromat",                  // 208
        "lawn",                        // 209
        "lecture_room",                // 210
        "legislative_chamber",         // 211
        "library_indoor",              // 212
        "library_outdoor",             // 213
        "lighthouse",                  // 214
        "living_room",                 // 215
        "loading_dock",                // 216
        "lobby",                       // 217
        "lock_chamber",                // 218
        "locker_room",                 // 219
        "mansion",                     // 220
        "manufactured_home",           // 221
        "market_indoor",               // 222
        "market_outdoor",              // 223
        "marsh",                       // 224
        "martial_arts_gym",            // 225
        "mausoleum",                   // 226
        "medina",                      // 227
        "mezzanine",                   // 228
        "moat_water",                  // 229
        "mosque_outdoor",              // 230
        "motel",                       // 231
        "mountain",                    // 232
        "mountain_path",               // 233
        "mountain_snowy",              // 234
        "movie_theater_indoor",        // 235
        "museum_indoor",               // 236
        "museum_outdoor",              // 237
        "music_studio",                // 238
        "natural_history_museum",      // 239
        "nursery",                     // 240
        "nursing_home",                // 241
        "oast_house",                  // 242
        "ocean",                       // 243
        "office",                      // 244
        "office_building",             // 245
        "office_cubicles",             // 246
        "oilrig",                      // 247
        "operating_room",              // 248
        "orchard",                     // 249
        "orchestra_pit",               // 250
        "pagoda",                      // 251
        "palace",                      // 252
        "pantry",                      // 253
        "park",                        // 254
        "parking_garage_indoor",       // 255
        "parking_garage_outdoor",      // 256
        "parking_lot",                 // 257
        "pasture",                     // 258
        "patio",                       // 259
        "pavilion",                    // 260
        "pet_shop",                    // 261
        "pharmacy",                    // 262
        "phone_booth",                 // 263
        "physics_laboratory",          // 264
        "picnic_area",                 // 265
        "pier",                        // 266
        "pizzeria",                    // 267
        "playground",                  // 268
        "playroom",                    // 269
        "plaza",                       // 270
        "pond",                        // 271
        "porch",                       // 272
        "promenade",                   // 273
        "pub_indoor",                  // 274
        "racecourse",                  // 275
        "raceway",                     // 276
        "raft",                        // 277
        "railroad_track",              // 278
        "rainforest",                  // 279
        "reception",                   // 280
        "recreation_room",             // 281
        "repair_shop",                 // 282
        "residential_neighborhood",    // 283
        "restaurant",                  // 284
        "restaurant_kitchen",          // 285
        "restaurant_patio",            // 286
        "rice_paddy",                  // 287
        "river",                       // 288
        "rock_arch",                   // 289
        "roof_garden",                 // 290
        "rope_bridge",                 // 291
        "ruin",                        // 292
        "runway",                      // 293
        "sandbox",                     // 294
        "sauna",                       // 295
        "schoolhouse",                 // 296
        "science_museum",              // 297
        "server_room",                 // 298
        "shed",                        // 299
        "shoe_shop",                   // 300
        "shopfront",                   // 301
        "shopping_mall_indoor",        // 302
        "shower",                      // 303
        "ski_resort",                  // 304
        "ski_slope",                   // 305
        "sky",                         // 306
        "skyscraper",                  // 307
        "slum",                        // 308
        "snowfield",                   // 309
        "soccer_field",                // 310
        "stable",                      // 311
        "stadium_baseball",            // 312
        "stadium_football",            // 313
        "stadium_soccer",              // 314
        "stage_indoor",                // 315
        "stage_outdoor",               // 316
        "staircase",                   // 317
        "storage_room",                // 318
        "street",                      // 319
        "subway_station_platform",     // 320
        "supermarket",                 // 321
        "sushi_bar",                   // 322
        "swamp",                       // 323
        "swimming_hole",               // 324
        "swimming_pool_indoor",        // 325
        "swimming_pool_outdoor",       // 326
        "synagogue_outdoor",           // 327
        "television_room",             // 328
        "television_studio",           // 329
        "temple_asia",                 // 330
        "throne_room",                 // 331
        "ticket_booth",                // 332
        "topiary_garden",              // 333
        "tower",                       // 334
        "toyshop",                     // 335
        "train_interior",              // 336
        "train_station_platform",      // 337
        "tree_farm",                   // 338
        "tree_house",                  // 339
        "trench",                      // 340
        "tundra",                      // 341
        "underwater_ocean_deep",       // 342
        "utility_room",                // 343
        "valley",                      // 344
        "vegetable_garden",            // 345
        "veterinarians_office",        // 346
        "viaduct",                     // 347
        "village",                     // 348
        "vineyard",                    // 349
        "volcano",                     // 350
        "volleyball_court_outdoor",    // 351
        "waiting_room",                // 352
        "water_park",                  // 353
        "water_tower",                 // 354
        "waterfall",                   // 355
        "watering_hole",               // 356
        "wave",                        // 357
        "wet_bar",                     // 358
        "wheat_field",                 // 359
        "wind_farm",                   // 360
        "windmill",                    // 361
        "yard",                        // 362
        "youth_hostel",                // 363
        "zen_garden",                  // 364
    ];

    /// <summary>
    /// Mappa indice Places365 → lista di (PhotoCategory, peso relativo).
    /// Le classi non presenti qui non contribuiscono ad alcuna categoria.
    /// I pesi determinano quanto una classe Places365 è rappresentativa della PhotoCategory:
    ///  1.0 = corrispondenza diretta
    ///  0.7 = buona corrispondenza
    ///  0.4 = corrispondenza parziale
    /// </summary>
    internal static readonly Dictionary<int, (string Category, float Weight)[]> Mapping = new()
    {
        // ===== MARE E SPIAGGIA =====
        [48]  = [(PhotoCategories.Beach, 1.0f)],                                                     // beach
        [49]  = [(PhotoCategories.Beach, 0.8f)],                                                     // beach_house
        [57]  = [(PhotoCategories.Beach, 0.7f)],                                                     // boardwalk
        [97]  = [(PhotoCategories.Beach, 0.9f)],                                                     // coast
        [171] = [(PhotoCategories.Beach, 0.6f), (PhotoCategories.City, 0.3f)],                       // harbor
        [194] = [(PhotoCategories.Beach, 0.7f), (PhotoCategories.Nature, 0.5f)],                     // islet
        [204] = [(PhotoCategories.Beach, 0.6f), (PhotoCategories.Nature, 0.6f)],                     // lagoon
        [243] = [(PhotoCategories.Beach, 0.8f), (PhotoCategories.Nature, 0.5f)],                     // ocean
        [266] = [(PhotoCategories.Beach, 0.7f)],                                                     // pier
        [324] = [(PhotoCategories.Beach, 0.5f), (PhotoCategories.Nature, 0.4f)],                     // swimming_hole
        [326] = [(PhotoCategories.Beach, 0.4f)],                                                     // swimming_pool_outdoor
        [342] = [(PhotoCategories.Beach, 0.5f), (PhotoCategories.Nature, 0.5f)],                     // underwater_ocean_deep
        [353] = [(PhotoCategories.Beach, 0.5f)],                                                     // water_park
        [357] = [(PhotoCategories.Beach, 0.9f), (PhotoCategories.Nature, 0.5f)],                     // wave

        // ===== MONTAGNA =====
        [30]  = [(PhotoCategories.Mountain, 0.5f), (PhotoCategories.Nature, 0.7f)],                  // badlands
        [73]  = [(PhotoCategories.Mountain, 0.7f), (PhotoCategories.Nature, 0.6f)],                  // butte
        [81]  = [(PhotoCategories.Mountain, 0.8f), (PhotoCategories.Nature, 0.7f)],                  // canyon
        [94]  = [(PhotoCategories.Mountain, 0.8f), (PhotoCategories.Nature, 0.6f)],                  // cliff
        [111] = [(PhotoCategories.Mountain, 0.7f), (PhotoCategories.Snow, 0.5f)],                    // crevasse
        [163] = [(PhotoCategories.Mountain, 0.6f), (PhotoCategories.Snow, 0.9f)],                    // glacier
        [232] = [(PhotoCategories.Mountain, 1.0f), (PhotoCategories.Nature, 0.5f)],                  // mountain
        [233] = [(PhotoCategories.Mountain, 0.9f), (PhotoCategories.Nature, 0.6f)],                  // mountain_path
        [234] = [(PhotoCategories.Mountain, 0.9f), (PhotoCategories.Snow, 0.8f)],                    // mountain_snowy
        [289] = [(PhotoCategories.Mountain, 0.6f), (PhotoCategories.Nature, 0.7f)],                  // rock_arch
        [344] = [(PhotoCategories.Mountain, 0.6f), (PhotoCategories.Nature, 0.8f)],                  // valley
        [350] = [(PhotoCategories.Mountain, 0.8f), (PhotoCategories.Nature, 0.7f)],                  // volcano

        // ===== NATURA E PAESAGGI =====
        [36]  = [(PhotoCategories.Nature, 0.9f)],                                                    // bamboo_forest
        [62]  = [(PhotoCategories.Nature, 0.9f)],                                                    // botanical_garden
        [76]  = [(PhotoCategories.Nature, 0.7f), (PhotoCategories.Mountain, 0.3f)],                  // campsite
        [78]  = [(PhotoCategories.Nature, 0.8f)],                                                    // canal_natural
        [104] = [(PhotoCategories.Nature, 0.8f)],                                                    // corn_field
        [110] = [(PhotoCategories.Nature, 0.9f)],                                                    // creek
        [116] = [(PhotoCategories.Nature, 0.8f)],                                                    // desert_sand
        [117] = [(PhotoCategories.Nature, 0.8f)],                                                    // desert_vegetation
        [138] = [(PhotoCategories.Nature, 0.7f)],                                                    // farm
        [140] = [(PhotoCategories.Nature, 0.8f)],                                                    // field_cultivated
        [141] = [(PhotoCategories.Nature, 0.9f)],                                                    // field_wild
        [145] = [(PhotoCategories.Nature, 0.7f)],                                                    // fishpond
        [150] = [(PhotoCategories.Nature, 1.0f)],                                                    // forest_broadleaf
        [151] = [(PhotoCategories.Nature, 0.9f)],                                                    // forest_path
        [152] = [(PhotoCategories.Nature, 0.8f)],                                                    // forest_road
        [153] = [(PhotoCategories.Nature, 0.7f)],                                                    // formal_garden
        [154] = [(PhotoCategories.Nature, 0.4f), (PhotoCategories.City, 0.4f)],                      // fountain
        [173] = [(PhotoCategories.Nature, 0.8f)],                                                    // hayfield
        [180] = [(PhotoCategories.Nature, 0.7f)],                                                    // hot_spring
        [197] = [(PhotoCategories.Nature, 0.7f)],                                                    // japanese_garden
        [205] = [(PhotoCategories.Nature, 0.9f)],                                                    // lake_natural
        [209] = [(PhotoCategories.Nature, 0.6f)],                                                    // lawn
        [224] = [(PhotoCategories.Nature, 0.8f)],                                                    // marsh
        [249] = [(PhotoCategories.Nature, 0.7f)],                                                    // orchard
        [254] = [(PhotoCategories.Nature, 0.6f), (PhotoCategories.City, 0.3f)],                      // park
        [258] = [(PhotoCategories.Nature, 0.8f)],                                                    // pasture
        [265] = [(PhotoCategories.Nature, 0.6f)],                                                    // picnic_area
        [271] = [(PhotoCategories.Nature, 0.8f)],                                                    // pond
        [279] = [(PhotoCategories.Nature, 1.0f)],                                                    // rainforest
        [287] = [(PhotoCategories.Nature, 0.8f)],                                                    // rice_paddy
        [288] = [(PhotoCategories.Nature, 0.9f)],                                                    // river
        [323] = [(PhotoCategories.Nature, 0.7f)],                                                    // swamp
        [333] = [(PhotoCategories.Nature, 0.6f)],                                                    // topiary_garden
        [338] = [(PhotoCategories.Nature, 0.7f)],                                                    // tree_farm
        [341] = [(PhotoCategories.Nature, 0.7f), (PhotoCategories.Snow, 0.4f)],                      // tundra
        [345] = [(PhotoCategories.Nature, 0.6f)],                                                    // vegetable_garden
        [349] = [(PhotoCategories.Nature, 0.8f)],                                                    // vineyard
        [355] = [(PhotoCategories.Nature, 0.9f), (PhotoCategories.Mountain, 0.4f)],                  // waterfall
        [356] = [(PhotoCategories.Nature, 0.7f)],                                                    // watering_hole
        [359] = [(PhotoCategories.Nature, 0.8f)],                                                    // wheat_field
        [364] = [(PhotoCategories.Nature, 0.7f)],                                                    // zen_garden

        // ===== NEVE E INVERNO =====
        [87]  = [(PhotoCategories.Snow, 0.5f), (PhotoCategories.Mountain, 0.6f)],                    // chalet
        [186] = [(PhotoCategories.Snow, 0.9f)],                                                      // ice_floe
        [187] = [(PhotoCategories.Snow, 0.9f)],                                                      // ice_shelf
        [188] = [(PhotoCategories.Snow, 0.5f), (PhotoCategories.Sport, 0.5f)],                       // ice_skating_rink_indoor
        [189] = [(PhotoCategories.Snow, 0.6f), (PhotoCategories.Sport, 0.4f)],                       // ice_skating_rink_outdoor
        [190] = [(PhotoCategories.Snow, 0.9f), (PhotoCategories.Nature, 0.4f)],                      // iceberg
        [191] = [(PhotoCategories.Snow, 0.8f)],                                                      // igloo
        [304] = [(PhotoCategories.Snow, 0.8f), (PhotoCategories.Sport, 0.6f)],                       // ski_resort
        [305] = [(PhotoCategories.Snow, 0.8f), (PhotoCategories.Sport, 0.7f)],                       // ski_slope
        [309] = [(PhotoCategories.Snow, 1.0f)],                                                      // snowfield

        // ===== CITTÀ E ARCHITETTURA =====
        [4]   = [(PhotoCategories.City, 0.8f)],                                                      // alley
        [8]   = [(PhotoCategories.City, 0.8f)],                                                      // apartment_building_outdoor
        [10]  = [(PhotoCategories.City, 0.7f)],                                                      // aqueduct
        [11]  = [(PhotoCategories.City, 0.6f)],                                                      // arcade
        [12]  = [(PhotoCategories.City, 0.8f)],                                                      // arch
        [25]  = [(PhotoCategories.City, 0.5f)],                                                      // atrium_public
        [66]  = [(PhotoCategories.City, 0.6f)],                                                      // bridge
        [67]  = [(PhotoCategories.City, 0.9f)],                                                      // building_facade
        [79]  = [(PhotoCategories.City, 0.7f)],                                                      // canal_urban
        [84]  = [(PhotoCategories.City, 0.7f)],                                                      // castle
        [91]  = [(PhotoCategories.City, 0.6f)],                                                      // church_outdoor
        [103] = [(PhotoCategories.City, 0.5f)],                                                      // construction_site
        [109] = [(PhotoCategories.City, 0.5f)],                                                      // courtyard
        [112] = [(PhotoCategories.City, 0.7f)],                                                      // crosswalk
        [125] = [(PhotoCategories.City, 0.9f)],                                                      // downtown
        [175] = [(PhotoCategories.City, 0.5f)],                                                      // highway
        [192] = [(PhotoCategories.City, 0.6f)],                                                      // industrial_area
        [200] = [(PhotoCategories.City, 0.6f)],                                                      // kasbah
        [214] = [(PhotoCategories.City, 0.5f), (PhotoCategories.Beach, 0.4f)],                       // lighthouse
        [220] = [(PhotoCategories.City, 0.6f)],                                                      // mansion
        [227] = [(PhotoCategories.City, 0.7f)],                                                      // medina
        [230] = [(PhotoCategories.City, 0.6f)],                                                      // mosque_outdoor
        [237] = [(PhotoCategories.City, 0.5f)],                                                      // museum_outdoor
        [245] = [(PhotoCategories.City, 0.7f)],                                                      // office_building
        [251] = [(PhotoCategories.City, 0.6f)],                                                      // pagoda
        [252] = [(PhotoCategories.City, 0.7f)],                                                      // palace
        [270] = [(PhotoCategories.City, 0.8f)],                                                      // plaza
        [273] = [(PhotoCategories.City, 0.6f)],                                                      // promenade
        [283] = [(PhotoCategories.City, 0.6f)],                                                      // residential_neighborhood
        [292] = [(PhotoCategories.City, 0.5f)],                                                      // ruin
        [307] = [(PhotoCategories.City, 1.0f)],                                                      // skyscraper
        [319] = [(PhotoCategories.City, 0.9f)],                                                      // street
        [327] = [(PhotoCategories.City, 0.5f)],                                                      // synagogue_outdoor
        [330] = [(PhotoCategories.City, 0.6f)],                                                      // temple_asia
        [334] = [(PhotoCategories.City, 0.7f)],                                                      // tower
        [347] = [(PhotoCategories.City, 0.6f)],                                                      // viaduct
        [348] = [(PhotoCategories.City, 0.5f), (PhotoCategories.Nature, 0.3f)],                      // village
        [361] = [(PhotoCategories.City, 0.5f), (PhotoCategories.Nature, 0.3f)],                      // windmill

        // ===== CIBO E RISTORANTE =====
        [31]  = [(PhotoCategories.Food, 0.8f)],                                                      // bakery_shop
        [39]  = [(PhotoCategories.Food, 0.5f)],                                                      // bar
        [53]  = [(PhotoCategories.Food, 0.5f)],                                                      // beer_garden
        [54]  = [(PhotoCategories.Food, 0.5f)],                                                      // beer_hall
        [72]  = [(PhotoCategories.Food, 0.5f)],                                                      // butchers_shop
        [75]  = [(PhotoCategories.Food, 0.5f)],                                                      // cafeteria
        [99]  = [(PhotoCategories.Food, 0.6f)],                                                      // coffee_shop
        [114] = [(PhotoCategories.Food, 0.7f)],                                                      // delicatessen
        [119] = [(PhotoCategories.Food, 0.7f)],                                                      // diner_outdoor
        [120] = [(PhotoCategories.Food, 0.7f)],                                                      // dining_hall
        [121] = [(PhotoCategories.Food, 0.6f)],                                                      // dining_room
        [139] = [(PhotoCategories.Food, 0.8f)],                                                      // fastfood_restaurant
        [148] = [(PhotoCategories.Food, 0.8f)],                                                      // food_court
        [155] = [(PhotoCategories.Food, 0.5f)],                                                      // galley
        [185] = [(PhotoCategories.Food, 0.6f)],                                                      // ice_cream_parlor
        [203] = [(PhotoCategories.Food, 0.4f)],                                                      // kitchen
        [267] = [(PhotoCategories.Food, 0.9f)],                                                      // pizzeria
        [274] = [(PhotoCategories.Food, 0.5f)],                                                      // pub_indoor
        [284] = [(PhotoCategories.Food, 1.0f)],                                                      // restaurant
        [285] = [(PhotoCategories.Food, 0.7f)],                                                      // restaurant_kitchen
        [286] = [(PhotoCategories.Food, 0.8f)],                                                      // restaurant_patio
        [322] = [(PhotoCategories.Food, 0.8f)],                                                      // sushi_bar
        [358] = [(PhotoCategories.Food, 0.4f)],                                                      // wet_bar

        // ===== CONCERTO =====
        [5]   = [(PhotoCategories.Concert, 0.6f)],                                                   // amphitheater
        [16]  = [(PhotoCategories.Concert, 0.8f)],                                                   // arena_performance
        [27]  = [(PhotoCategories.Concert, 0.7f)],                                                   // auditorium
        [122] = [(PhotoCategories.Concert, 0.8f), (PhotoCategories.Night, 0.4f)],                    // discotheque
        [238] = [(PhotoCategories.Concert, 0.6f)],                                                   // music_studio
        [250] = [(PhotoCategories.Concert, 0.7f)],                                                   // orchestra_pit
        [315] = [(PhotoCategories.Concert, 0.9f)],                                                   // stage_indoor
        [316] = [(PhotoCategories.Concert, 0.8f)],                                                   // stage_outdoor

        // ===== MATRIMONIO (le scene di matrimonio sono difficili da mappare, usiamo le più probabili) =====
        [35]  = [(PhotoCategories.Wedding, 0.6f), (PhotoCategories.BirthdayParty, 0.4f)],            // ballroom
        [38]  = [(PhotoCategories.Wedding, 0.7f), (PhotoCategories.BirthdayParty, 0.5f)],            // banquet_hall
        [90]  = [(PhotoCategories.Wedding, 0.5f)],                                                   // church_indoor

        // ===== FESTA E COMPLEANNO =====
        [6]   = [(PhotoCategories.BirthdayParty, 0.5f)],                                             // amusement_arcade
        [7]   = [(PhotoCategories.BirthdayParty, 0.6f)],                                             // amusement_park
        [34]  = [(PhotoCategories.BirthdayParty, 0.6f)],                                             // ball_pit
        [83]  = [(PhotoCategories.BirthdayParty, 0.4f)],                                             // carrousel
        [268] = [(PhotoCategories.BirthdayParty, 0.4f)],                                             // playground

        // ===== SPORT =====
        [15]  = [(PhotoCategories.Sport, 0.8f)],                                                     // arena_hockey
        [17]  = [(PhotoCategories.Sport, 0.6f)],                                                     // arena_rodeo
        [24]  = [(PhotoCategories.Sport, 0.7f)],                                                     // athletic_field_outdoor
        [42]  = [(PhotoCategories.Sport, 0.9f)],                                                     // baseball_field
        [44]  = [(PhotoCategories.Sport, 0.9f)],                                                     // basketball_court_indoor
        [64]  = [(PhotoCategories.Sport, 0.7f)],                                                     // bowling_alley
        [65]  = [(PhotoCategories.Sport, 0.8f)],                                                     // boxing_ring
        [68]  = [(PhotoCategories.Sport, 0.7f)],                                                     // bullring
        [149] = [(PhotoCategories.Sport, 0.9f)],                                                     // football_field
        [164] = [(PhotoCategories.Sport, 0.7f), (PhotoCategories.Nature, 0.3f)],                     // golf_course
        [168] = [(PhotoCategories.Sport, 0.7f)],                                                     // gymnasium_indoor
        [225] = [(PhotoCategories.Sport, 0.7f)],                                                     // martial_arts_gym
        [275] = [(PhotoCategories.Sport, 0.8f), (PhotoCategories.Motors, 0.4f)],                     // racecourse
        [276] = [(PhotoCategories.Sport, 0.5f), (PhotoCategories.Motors, 0.8f)],                     // raceway
        [310] = [(PhotoCategories.Sport, 0.9f)],                                                     // soccer_field
        [312] = [(PhotoCategories.Sport, 0.9f)],                                                     // stadium_baseball
        [313] = [(PhotoCategories.Sport, 0.9f)],                                                     // stadium_football
        [314] = [(PhotoCategories.Sport, 0.9f)],                                                     // stadium_soccer
        [325] = [(PhotoCategories.Sport, 0.5f)],                                                     // swimming_pool_indoor
        [351] = [(PhotoCategories.Sport, 0.7f)],                                                     // volleyball_court_outdoor

        // ===== MOTORI E AUTO =====
        [28]  = [(PhotoCategories.Motors, 0.7f)],                                                    // auto_factory
        [29]  = [(PhotoCategories.Motors, 0.8f)],                                                    // auto_showroom
        [82]  = [(PhotoCategories.Motors, 0.6f)],                                                    // car_interior
        [158] = [(PhotoCategories.Motors, 0.5f)],                                                    // gas_station

        // ===== FOTOGRAFIA NOTTURNA =====
        // (la notte è difficile da mappare da scene — è più un fatto di luminosità e orario)
        // L'euristica EXIF/luminosità resta utile come boost

        // ===== TRAMONTO =====
        // (tramonto = rilevato meglio dall'euristica colorimetrica, il modello non ha una classe specifica)
        [306] = [(PhotoCategories.Sunset, 0.4f), (PhotoCategories.Nature, 0.3f)],                   // sky

        // ===== ANIMALI =====
        [9]   = [(PhotoCategories.Animals, 0.7f)],                                                   // aquarium
        [105] = [(PhotoCategories.Animals, 0.5f)],                                                   // corral
        [201] = [(PhotoCategories.Animals, 0.5f)],                                                   // kennel_outdoor
        [261] = [(PhotoCategories.Animals, 0.6f)],                                                   // pet_shop
        [311] = [(PhotoCategories.Animals, 0.5f)],                                                   // stable
        [346] = [(PhotoCategories.Animals, 0.5f)],                                                   // veterinarians_office
    };
}
