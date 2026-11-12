# Magenta key-pose judge (ff00ff)

Refs: concept `a_babf8d7bca383dae`, pose3 colours `a_0960962c0b655f36`.

| Pose | Asset | Verdict | Note |
|---|---|---|---|
| IdleSigh | a_8618b4b2b6e01956 | PASS | Bored sigh, closed helm, launcher, magenta plate |
| Walk | a_bf18806edfb4186b | PASS | Planted stride, cloth trails |
| Jump | a_c690fa811c5eb99d | PASS | Tuck, cloth streams |
| Fall | a_b1b6c4d42601039c | FAIL | Read as dash; motion lines; head/launcher mush — redo `j_39a4c3b0d5c2` |
| Land | a_9e728f3f1fd49c24 | PASS | Squash crouch, hand planted |
| Dash | a_9ffc4d582fa52f6d | PASS | Forward lean, cloth horizontal |
| Hurt | a_b803fdda7fd36e76 | FAIL | Open-faced helm shows a face (canon: closed helm) — redo `j_c6bc1f8affbb` |
| DeathBurn | a_52d8c1e3e6c36e72 | PASS | Kneeling, burn FX |
| Rewind | a_8bbc37df7ee3739c | PASS* | Weak rewind read (walk-like) but closed helm + colours OK; use for i2v |
| Victory | a_d7e1767c4d7a101f | PASS | Shrug, closed helm |

*Rewind is the weakest PASS; Fall/Hurt take the two redo slots.
