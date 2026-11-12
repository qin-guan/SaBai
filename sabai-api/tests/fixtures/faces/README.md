# Face photos for tests

Portraits of two people, used by `tests/api/faces.rs` and the pipeline check in
`src/face/analyzer.rs`: two different photos of one person and one photo of another.
All three were downloaded from Wikimedia Commons at 960 pixels wide
(`https://commons.wikimedia.org/wiki/Special:FilePath/<file>?width=640`) and are unmodified.

| File | Commons file | Author | Licence |
| --- | --- | --- | --- |
| `obama-2012.jpg` | `President_Barack_Obama.jpg` | Pete Souza, official White House photo (6 Dec 2012) | Public domain (work of the US federal government) |
| `obama-2009.jpg` | `Official_portrait_of_Barack_Obama.jpg` | Pete Souza, The Obama-Biden Transition Project (13 Jan 2009) | CC BY 3.0 |
| `biden-2021.jpg` | `Joe_Biden_presidential_portrait.jpg` | Adam Schultz, official White House photo (3 Mar 2021) | Public domain (work of the US federal government) |

They are here only to exercise face detection and matching.
