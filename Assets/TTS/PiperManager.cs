using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Sentis;
using UnityEngine;

namespace Piper
{
    public class PiperManager : MonoBehaviour
    {
        public BackendType backend = BackendType.GPUCompute;
        public ModelAsset model;

        public string voice = "en-us";
        public int sampleRate = 22050;

        private Model _runtimeModel;
        private Worker _worker;

        private void Awake()
        {
            string espeakPath = $"{Application.streamingAssetsPath}/espeak-ng-data";
            PiperWrapper.InitPiper(espeakPath);
            _runtimeModel = ModelLoader.Load(model);
            _worker = new Worker(_runtimeModel, backend);
        }

        public async Task<float[]> TextToSpeech(string text)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            Debug.Log("Piper Phonemize processing text...");
            var phonemes = PiperWrapper.ProcessText(text, voice);
            Debug.Log($"Piper Phonemize processed text: {sw.ElapsedMilliseconds} ms");

            Debug.Log("Starting Piper inference...");
            sw.Restart();

            var inputLengthsShape = new TensorShape(1);
            var scalesShape = new TensorShape(3);
            using Tensor<float> scalesTensor = new Tensor<float>(
                scalesShape,
                new float[] { 0.667f, 1f, 0.8f }
            );

            var audioBuffer = new List<float>();
            for (int i = 0; i < phonemes.Sentences.Length; i++)
            {
                var sentence = phonemes.Sentences[i];

                var inputPhonemes = sentence.PhonemesIds;
                var inputShape = new TensorShape(1, inputPhonemes.Length);
                using Tensor<int> inputTensor = new Tensor<int>(inputShape, inputPhonemes);
                using Tensor<int> inputLengthsTensor = new Tensor<int>(
                    inputLengthsShape,
                    new int[] { inputPhonemes.Length }
                );

                _worker.SetInput("input", inputTensor);
                _worker.SetInput("input_lengths", inputLengthsTensor);
                _worker.SetInput("scales", scalesTensor);
                _worker.Schedule();

                using Tensor<float> outputTensor = _worker.PeekOutput() as Tensor<float>;
                using Tensor<float> output = await outputTensor.ReadbackAndCloneAsync();
                audioBuffer.AddRange(output.DownloadToArray());
            }

            Debug.Log($"Finished piper inference: {sw.ElapsedMilliseconds} ms");
            sw.Restart();
            return audioBuffer.ToArray();
        }

        private void OnDestroy()
        {
            PiperWrapper.FreePiper();
            _worker.Dispose();
        }
    }
}
