# OOM on beta (build v246, 823af11)

Nothing new to fix. The report is from v246 (823af11), the art pass that put every hero grip sheet into GameDefinition's boot load, so the page ran out of memory (abort OOM, then "memory access out of bounds"). f06d8587 already fixed that: the deploy's PlayerDataSplit takes the found weapons' grip sheets out of the boot load and makes each its own addressable bundle, which `PlayerSheets` loads on demand. It is in this branch's history. The earlier reports #219, #220 and #221 had the same cause.

No code change. Not checked in a browser or a build (none here). J should look at the next beta build after f06d8587 (later than v246). If OOM still shows there, it needs a new report with that build number.
