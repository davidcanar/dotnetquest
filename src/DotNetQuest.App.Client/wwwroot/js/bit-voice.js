// Bit's voice — browser speech synthesis with EN/ES voice selection.
// Reports speaking state back to .NET so the mascot's mouth can animate.

let currentVoice = null;
let voicesLoaded = false;

function pickVoice(lang) {
    if (!('speechSynthesis' in window)) return null;
    const voices = speechSynthesis.getVoices();
    if (!voices.length) return null;
    // Exact match (e.g. es-ES, en-US), then language prefix, then default.
    return (
        voices.find(v => v.lang.replace('_', '-').toLowerCase() === lang.toLowerCase()) ||
        voices.find(v => v.lang.replace('_', '-').toLowerCase().startsWith(lang.slice(0, 2).toLowerCase())) ||
        voices.find(v => v.default) ||
        voices[0]
    );
}

if ('speechSynthesis' in window) {
    speechSynthesis.onvoiceschanged = () => { voicesLoaded = true; };
}

export function isSupported() {
    return 'speechSynthesis' in window;
}

export function speak(text, lang, dotnetRef) {
    if (!('speechSynthesis' in window) || !text) return;
    speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = lang;
    const voice = pickVoice(lang);
    if (voice) utterance.voice = voice;
    // Energetic robot: slightly higher pitch, slightly faster.
    utterance.pitch = 1.15;
    utterance.rate = 1.06;
    utterance.onstart = () => dotnetRef?.invokeMethodAsync('SetSpeaking', true);
    utterance.onend = () => dotnetRef?.invokeMethodAsync('SetSpeaking', false);
    utterance.onerror = () => dotnetRef?.invokeMethodAsync('SetSpeaking', false);
    speechSynthesis.speak(utterance);
}

export function stop() {
    if ('speechSynthesis' in window) speechSynthesis.cancel();
}
