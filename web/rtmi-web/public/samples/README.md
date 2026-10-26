# Synthetic referee test scenarios

Each scenario is an original sequence of eight PNG stick-frame images. The dashboard's **Roll Random Scenario** button picks a different scenario from the previously loaded one when possible. Goalkeepers wear gold shirts so they can be distinguished from the lime home and blue away outfield players.

| Folder | Scenario |
|---|---|
| [away-attack](./away-attack/) | Blue away side attacks left toward the lime home goal; gold-shirted goalkeeper blocks the shot. |
| [home-counterattack](./home-counterattack/) | Lime home side counterattacks right toward the blue away goal; gold-shirted goalkeeper dives to block. |
| [away-corner](./away-corner/) | Blue away side takes a corner toward the lime home goal; gold-shirted goalkeeper claims the cross. |
| [away-offside-goal](./away-offside-goal/) | Blue away forward is ahead of the second-last defender when the through-ball is played, then scores past the gold-shirted home goalkeeper. |

For Foundry testing, attach `frame-01.png` through `frame-08.png` in order from one selected scenario. These illustrations are synthetic and are not match footage or a real-world VAR benchmark. The offside sequence includes a visible defender reference line at the pass and the ball entering the goal; the sequence is an illustrative test case, not a real-world VAR benchmark.
