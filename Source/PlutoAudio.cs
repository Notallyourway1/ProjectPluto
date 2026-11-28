using System.Collections.Generic;
using UnityEngine;

namespace ProjectPluto
{
    // Every sound the plugin plays itself (the boosters and the ramjet use the part's EFFECTS):
    // clunks and alarms on the missile, warhead blasts that arrive at the speed of sound, and a Geiger
    // counter while the active craft's crew is being irradiated. All of it follows the game's ship
    // volume and pauses with the game.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class PlutoAudio : MonoBehaviour
    {
        public enum Cue { BoosterJettison, WarheadRelease, LauncherRelease, InletUnstart, ScramAlarm }

        private const int SampleRate = 44100;
        private const int MaxBooms = 6;
        // Geiger dose rate is smoothed over about this long.
        private const float GeigerSmoothingSeconds = 0.3f;

        private static AudioClip decoupler, debris, blastLarge, blastLow, alarm, click;

        public static PlutoAudio Instance { get; private set; }

        private sealed class Boom
        {
            public CelestialBody Body;
            public PlutoVec Position;
            public double BurstUT;
            public AudioSource Source;
            public AudioSource Rumble;
            public AudioLowPassFilter Filter;
            public bool Playing;
        }

        private readonly List<Boom> booms = new List<Boom>();
        private readonly List<AudioSource> partSources = new List<AudioSource>();
        private readonly Dictionary<Part, AudioSource> partSourceByPart = new Dictionary<Part, AudioSource>();
        private AudioSource geiger;
        private double geigerBucketSv;
        private float geigerRate;
        private bool paused;

        private void Awake()
        {
            Instance = this;
            LoadClips();
            geiger = MakeSource(gameObject, false);
            GameEvents.onGamePause.Add(OnPause);
            GameEvents.onGameUnpause.Add(OnUnpause);
        }

        private void OnDestroy()
        {
            GameEvents.onGamePause.Remove(OnPause);
            GameEvents.onGameUnpause.Remove(OnUnpause);
            foreach (Boom b in booms)
                DestroyBoom(b);
            booms.Clear();
            if (Instance == this)
                Instance = null;
        }

        // ---- Clips -------------------------------------------------------------------------------

        private static void LoadClips()
        {
            if (decoupler == null)
                decoupler = Stock("Squad/Sounds/sound_decoupler_fire");
            if (debris == null)
                debris = Stock("Squad/Sounds/sound_explosion_debris1");
            if (blastLarge == null)
                blastLarge = Stock("Squad/Sounds/sound_explosion_large");
            if (blastLow == null)
                blastLow = Stock("sound_explosion_low") ?? blastLarge;
            if (alarm == null)
                alarm = MakeAlarm();
            if (click == null)
                click = MakeClick();
        }

        private static AudioClip Stock(string url)
        {
            AudioClip clip = GameDatabase.Instance != null ? GameDatabase.Instance.GetAudioClip(url) : null;
            if (clip == null)
                Debug.LogWarning($"[ProjectPluto] Sound '{url}' not found; it will be silent.");
            return clip;
        }

        // Reactor SCRAM alarm: three rising-falling two-tone beeps, made here so no sound file is needed.
        private static AudioClip MakeAlarm()
        {
            const float beep = 0.22f, gap = 0.06f;
            int perBeep = (int)(SampleRate * (beep + gap));
            var data = new float[perBeep * 6];
            for (int b = 0; b < 6; b++)
            {
                double freq = b % 2 == 0 ? 960 : 720;
                for (int i = 0; i < (int)(SampleRate * beep); i++)
                {
                    double t = (double)i / SampleRate;
                    double envelope = System.Math.Min(1, System.Math.Min(t / 0.01, (beep - t) / 0.02));
                    // A square-ish tone (fundamental plus odd harmonics) cuts through engine noise.
                    double w = 2 * System.Math.PI * freq * t;
                    double s = System.Math.Sin(w) + System.Math.Sin(3 * w) / 3 + System.Math.Sin(5 * w) / 5;
                    data[b * perBeep + i] = (float)(0.45 * envelope * s);
                }
            }
            AudioClip clip = AudioClip.Create("Pluto SCRAM alarm", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // One Geiger-counter tick: a few milliseconds of sharply decaying noise.
        private static AudioClip MakeClick()
        {
            int length = (int)(SampleRate * 0.006);
            var data = new float[length];
            var random = new System.Random(7);
            for (int i = 0; i < length; i++)
            {
                double decay = System.Math.Exp(-i / (SampleRate * 0.0009));
                data[i] = (float)((random.NextDouble() * 2 - 1) * decay * 0.9);
            }
            AudioClip clip = AudioClip.Create("Pluto Geiger click", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---- Sounds on a part --------------------------------------------------------------------

        public static void Play(Part part, Cue cue)
        {
            if (Instance == null || part == null || !HighLogic.LoadedSceneIsFlight)
                return;
            switch (cue)
            {
                case Cue.BoosterJettison:
                    Instance.PlayOnPart(part, decoupler, 1f, 0.85f);
                    break;
                case Cue.WarheadRelease:
                    Instance.PlayOnPart(part, decoupler, 0.55f, 1.35f);
                    break;
                case Cue.LauncherRelease:
                    Instance.PlayOnPart(part, decoupler, 0.8f, 0.7f);
                    break;
                case Cue.InletUnstart:
                    Instance.PlayOnPart(part, debris, 1f, 0.6f);
                    Instance.PlayOnPart(part, blastLow, 0.5f, 1.6f);
                    break;
                case Cue.ScramAlarm:
                    Instance.PlayOnPart(part, alarm, 0.7f, 1f);
                    break;
            }
        }

        private void PlayOnPart(Part part, AudioClip clip, float volume, float pitch)
        {
            if (clip == null || paused)
                return;
            if (!partSourceByPart.TryGetValue(part, out AudioSource source) || source == null)
            {
                PruneParts();
                // A child of the part, so it moves with it through floating-origin shifts.
                var node = new GameObject("Pluto sound");
                node.transform.SetParent(part.transform, false);
                source = MakeSource(node, true);
                partSourceByPart[part] = source;
                partSources.Add(source);
            }
            // PlayOneShot takes no pitch, so overlapping cues at different pitches share the last one set;
            // they are short and rarely overlap.
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * GameSettings.SHIP_VOLUME);
        }

        private static AudioSource MakeSource(GameObject node, bool spatial)
        {
            AudioSource source = node.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 25f;
            source.maxDistance = 4000f;
            return source;
        }

        // ---- Warhead blasts ----------------------------------------------------------------------

        // Heard only in air, and only once the blast's sound front reaches the listener.
        public static void Blast(Vector3 worldPosition, CelestialBody body)
        {
            if (Instance == null || body == null || blastLarge == null)
                return;
            double altitude = body.GetAltitude(worldPosition);
            if (PlutoDetonationEffect.IsVacuum(body, altitude))
                return;
            List<Boom> list = Instance.booms;
            if (list.Count >= MaxBooms)
            {
                Instance.DestroyBoom(list[0]);
                list.RemoveAt(0);
            }
            list.Add(new Boom
            {
                Body = body,
                Position = PlutoFallingObject.ToLocalPoint(body, worldPosition),
                BurstUT = Planetarium.GetUniversalTime(),
            });
        }

        private void UpdateBooms()
        {
            if (booms.Count == 0)
                return;
            double now = Planetarium.GetUniversalTime();
            bool inCabin = InCabin();
            Vessel active = FlightGlobals.ActiveVessel;
            Vector3 listener = ListenerPosition(active, inCabin);
            Transform view = ViewTransform();
            for (int i = booms.Count - 1; i >= 0; i--)
            {
                Boom b = booms[i];
                double age = now - b.BurstUT;
                double speed = active != null && active.mainBody == b.Body ? active.speedOfSound : 0;
                Vector3 world = PlutoFallingObject.ToWorldPoint(b.Body, b.Position);
                double distance = (world - listener).magnitude;

                if (!b.Playing)
                {
                    if (PlutoSoundModel.BoomExpired(age, speed) || active == null || active.mainBody != b.Body)
                    {
                        booms.RemoveAt(i);
                        continue;
                    }
                    if (!PlutoSoundModel.BoomArrived(distance, age, speed))
                        continue;
                    // A listener above the air hears nothing.
                    if (ListenerInVacuum(active, listener) || PlutoSoundModel.BoomLoudness(distance) <= 0.001)
                    {
                        booms.RemoveAt(i);
                        continue;
                    }
                    StartBoom(b, distance, inCabin);
                }
                else if (b.Source == null || (!paused && !b.Source.isPlaying && (b.Rumble == null || !b.Rumble.isPlaying)))
                {
                    DestroyBoom(b);
                    booms.RemoveAt(i);
                    continue;
                }
                Steer(b, world, listener, view, distance, inCabin);
            }
        }

        private void StartBoom(Boom b, double distance, bool inCabin)
        {
            var node = new GameObject("Pluto blast sound");
            node.transform.SetParent(transform, false);
            b.Source = MakeSource(node, false);
            b.Rumble = MakeSource(node, false);
            b.Filter = node.AddComponent<AudioLowPassFilter>();
            b.Source.clip = blastLarge;
            b.Source.pitch = 0.55f;
            b.Rumble.clip = blastLow;
            b.Rumble.pitch = 0.4f;
            b.Playing = true;
            b.Source.Play();
            if (b.Rumble.clip != null)
                b.Rumble.Play();
            // The arriving blast wave also shakes the camera, gently and only nearby.
            double loudness = PlutoSoundModel.BoomLoudness(distance);
            if (!inCabin && distance < 20000)
                PlutoShockShake.Play((float)(0.6 * loudness), 0.9f, 1.4f);
        }

        // Loudness, muffling and left-right placement, kept up to date as the listener moves.
        private static void Steer(Boom b, Vector3 world, Vector3 listener, Transform view, double distance, bool inCabin)
        {
            float loudness = (float)PlutoSoundModel.BoomLoudness(distance) * GameSettings.SHIP_VOLUME;
            float pan = 0f;
            if (view != null && distance > 1)
                pan = 0.7f * Vector3.Dot((world - listener) / (float)distance, view.right);
            b.Source.volume = loudness;
            b.Source.panStereo = pan;
            if (b.Rumble != null)
            {
                b.Rumble.volume = loudness * 0.9f;
                b.Rumble.panStereo = pan;
            }
            if (b.Filter != null)
                b.Filter.cutoffFrequency = (float)PlutoSoundModel.BoomCutoffHz(distance, inCabin);
        }

        private void DestroyBoom(Boom b)
        {
            if (b.Source != null)
                Destroy(b.Source.gameObject);
            b.Source = null;
            b.Rumble = null;
            b.Filter = null;
        }

        private static bool InCabin()
        {
            CameraManager manager = CameraManager.Instance;
            return manager != null && (manager.currentCameraMode == CameraManager.CameraMode.IVA
                || manager.currentCameraMode == CameraManager.CameraMode.Internal);
        }

        // Where the player hears from: the flight camera, or the craft itself when inside the cabin.
        private static Vector3 ListenerPosition(Vessel active, bool inCabin)
        {
            if (inCabin && active != null)
                return active.transform.position;
            Camera camera = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
            if (camera != null)
                return camera.transform.position;
            return active != null ? active.transform.position : Vector3.zero;
        }

        private static Transform ViewTransform()
        {
            Camera camera = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : null;
            return camera != null ? camera.transform : null;
        }

        private static bool ListenerInVacuum(Vessel active, Vector3 listener)
        {
            CelestialBody body = active.mainBody;
            return PlutoDetonationEffect.IsVacuum(body, body.GetAltitude(listener));
        }

        // ---- Geiger counter ----------------------------------------------------------------------

        // Called by the radiation code with the highest dose any one crewed part of the active craft
        // took from one source in one dosing step.
        public static void ReportActiveDose(double sv)
        {
            if (Instance != null && sv > 0)
                Instance.geigerBucketSv += sv;
        }

        private void FixedUpdate()
        {
            float dt = TimeWarp.fixedDeltaTime;
            if (dt <= 0)
                return;
            float instant = (float)(geigerBucketSv / dt);
            geigerBucketSv = 0;
            geigerRate += (instant - geigerRate) * (1f - Mathf.Exp(-dt / GeigerSmoothingSeconds));
        }

        private void UpdateGeiger()
        {
            if (click == null || paused || Time.deltaTime <= 0)
                return;
            double rate = PlutoSoundModel.GeigerClicksPerSecond(geigerRate);
            if (rate <= 0)
                return;
            // Random ticks at the wanted average rate; several can fall in one frame.
            double expected = rate * Time.deltaTime;
            int ticks = (int)expected + (Random.value < expected - (int)expected ? 1 : 0);
            for (int i = 0; i < ticks; i++)
            {
                geiger.pitch = Random.Range(0.85f, 1.2f);
                geiger.PlayOneShot(click, 0.5f * GameSettings.SHIP_VOLUME);
            }
        }

        // ---- Frame and pause ---------------------------------------------------------------------

        private void Update()
        {
            if (paused)
                return;
            UpdateBooms();
            UpdateGeiger();
        }

        private void OnPause() => SetPaused(true);
        private void OnUnpause() => SetPaused(false);

        private void SetPaused(bool pause)
        {
            paused = pause;
            for (int i = partSources.Count - 1; i >= 0; i--)
            {
                if (partSources[i] == null)
                {
                    partSources.RemoveAt(i);
                    continue;
                }
                Pause(partSources[i], pause);
            }
            foreach (Boom b in booms)
            {
                Pause(b.Source, pause);
                Pause(b.Rumble, pause);
            }
            Pause(geiger, pause);
        }

        private static void Pause(AudioSource source, bool pause)
        {
            if (source == null)
                return;
            if (pause)
                source.Pause();
            else
                source.UnPause();
        }

        private readonly List<Part> deadParts = new List<Part>();

        // Forgets parts that are gone so the lookup does not grow through a long flight.
        private void PruneParts()
        {
            deadParts.Clear();
            foreach (var entry in partSourceByPart)
            {
                if (entry.Key == null || entry.Value == null)
                    deadParts.Add(entry.Key);
            }
            foreach (Part p in deadParts)
                partSourceByPart.Remove(p);
        }
    }
}
