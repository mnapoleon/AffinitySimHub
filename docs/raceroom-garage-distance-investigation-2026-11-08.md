# RaceRoom distance jumps after leaving the garage

On November 8, 2026, Affinity recorded 130.21 km for the Alpine A110 GT4+ on Nürburgring - Nordschleife-Tourist. The driver reported starting four laps without completing four. RaceRoom's final logged lap counter was one, and its session odometer read 30.70 km.

All five matching rows in the live Affinity database came from this outing. The two most recent backups also had zero rows for this car and layout.

| Database row | Segment start (EST) | Saved distance |
| --- | --- | ---: |
| 1217 | 9:31:21 AM | 6.144 km |
| 1218 | 9:34:19 AM | 7.336 km |
| 1219 | 9:37:40 AM | 19.228 km |
| 1220 | 9:46:17 AM | 46.618 km |
| 1221 | 9:48:41 AM | 50.889 km |

The debug log shows Affinity retained distance already driven when the driver returned to the garage and ignored telemetry flagged as in-garage. The first active update of each of the last two segments added 41,570.16 m while speed was below 1 km/h. Both updates had a zero distance origin, one completed lap carried over from earlier in the outing, and a reported track position of 20,784.58 m on a 20,785.58 m layout. The two starting jumps account for 83.14 km of the saved total. Removing those two jumps alone would not reconcile the total with RaceRoom's odometer.

To reproduce, complete a lap in RaceRoom's Tourist layout, start another attempt, then return to the garage mid-lap and drive out again without resetting the RaceRoom session. Check the first active sample after each departure and the saved segment distance. Affinity should retain the distance driven before the reset and count only newly driven distance after departure.

Evidence came from `Affinity.distance.db` and `Affinity.distance.debug.raceroomracingexperience.log` in SimHub's `PluginsData/Affinity` folder. The two jumps were logged at `14:46:17.5521793Z` and `14:48:41.5696517Z` under RaceRoom session ID `c512581c-5623-4e31-937e-b33b1115dd9e`.
