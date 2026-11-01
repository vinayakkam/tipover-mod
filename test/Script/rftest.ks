// RocketFailure in-game test. Runs one test per launch from the pad:
//   tip    : sit on pad (must survive), hop, soft landing (must survive), tip over with reaction wheel (must explode)
//   impact : hop, descend at 5.5 m/s (must explode; needs impactSpeed < 5.5 in settings.cfg, stock parts break at 7)
// After each test: Revert to Launch and the next one runs. Results go to Ships/Script/rftest_log.txt.
@lazyglobal off.
clearscreen.
local logf is "0:/rftest_log.txt".
local modef is "0:/rftest_mode.txt".
local targetVS is 0.

function say {
    parameter msg.
    local line is "[" + round(missiontime, 1) + "] " + msg.
    print line.
    log line to logf.
}

function tilt { return vang(ship:facing:forevector, up:vector). }

// Holds vertical speed at targetVS: gravity feed-forward plus proportional correction.
function hoverThrottle {
    local maxAcc is ship:availablethrust / ship:mass.
    if maxAcc <= 0 { return 0. }
    local g is body:mu / (body:radius + altitude) ^ 2.
    return max(0, min(1, (g + (targetVS - ship:verticalspeed)) / maxAcc)).
}

// Lift off, climb to `height` m above the start, then descend at `descentVS` until landed.
// Returns the vertical speed just before touchdown.
function hopAndLand {
    parameter height, descentVS.
    local r0 is alt:radar.
    lock steering to heading(90, 90).
    set targetVS to 4.
    lock throttle to hoverThrottle().
    wait until stage:ready.
    stage.
    wait until alt:radar > r0 + height.
    set targetVS to 0.
    wait 2.
    set targetVS to descentVS.
    local vs is 0.
    until ship:status = "LANDED" or ship:status = "SPLASHED" {
        set vs to ship:verticalspeed.
        wait 0.
    }
    lock throttle to 0.
    return vs.
}

if ship:status <> "PRELAUNCH" {
    print "RF-Test: not on the pad, nothing to do.".
} else {
    local mode is "tip".
    if exists(modef) { set mode to open(modef):readall:string:trim. }
    local nextMode is "done".
    if mode = "tip" { set nextMode to "impact". }
    if exists(modef) { deletepath(modef). }
    log nextMode to modef.

    if mode = "tip" {
        say("=== TEST tip ===").
        wait 6.
        say("PASS pad: alive after 6 s on pad, status " + ship:status).
        local vs is hopAndLand(8, -1.5).
        say("touchdown at " + round(vs, 2) + " m/s, tilt " + round(tilt(), 1)).
        wait 8.
        say("PASS soft landing: alive 8 s after touchdown, tilt " + round(tilt(), 1) + ", status " + ship:status).
        say("tipping with reaction wheel").
        unlock steering.
        set ship:control:pitch to 1.
        local t0 is time:seconds.
        local over is -1.
        until time:seconds - t0 > 40 {
            if over < 0 and tilt() > 60 { set over to time:seconds. say("tilt passed 60 deg, status " + ship:status). }
            if over > 0 and time:seconds - over > 15 { break. }
            wait 0.5.
        }
        set ship:control:neutralize to true.
        if over < 0 { say("FAIL tip: could not tip past 60 deg (tilt " + round(tilt(), 1) + ")"). }
        else { say("FAIL tip: still alive 15 s after tipping, tilt " + round(tilt(), 1) + ", status " + ship:status). }
    } else if mode = "impact" {
        say("=== TEST impact ===").
        wait 3.
        local vs is hopAndLand(15, -5.5).
        say("touchdown at " + round(vs, 2) + " m/s").
        wait 10.
        say("FAIL impact: alive 10 s after touchdown at " + round(vs, 2) + " m/s, status " + ship:status).
    } else {
        print "RF-Test: all tests done. Delete Ships/Script/rftest_mode.txt to run them again.".
    }
}
