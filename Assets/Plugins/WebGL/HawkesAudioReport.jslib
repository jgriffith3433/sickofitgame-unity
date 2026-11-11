// The game's own sound starts for Studio's audio probe (hawkes-audio.js listens for `hawkes-sound` window events).
// Harmless on a page without the probe: the events just go nowhere.
mergeInto(LibraryManager.library, {
    HawkesReportSoundStart: function (sample, bus, rate, gain, dur) {
        try {
            var detail = { type: 'sound-start', sample: UTF8ToString(sample), bus: UTF8ToString(bus), rate: rate, gain: gain };
            if (dur > 0) detail.dur = dur;
            window.dispatchEvent(new CustomEvent('hawkes-sound', { detail: detail }));
        } catch (e) { }
    },

    HawkesReportBusGain: function (bus, gain) {
        try {
            window.dispatchEvent(new CustomEvent('hawkes-sound', { detail: { type: 'bus-gain', bus: UTF8ToString(bus), gain: gain } }));
        } catch (e) { }
    }
});
