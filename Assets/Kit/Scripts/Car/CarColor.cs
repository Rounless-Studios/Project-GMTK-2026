using UnityEngine;
/// <summary>
/// use the color selected by player (using CarColorPickerGUI script) to apply it to the car body paint renderer
/// else, if useRandom it will create one (used for all AI cars)
/// </summary>
namespace GMTK.Kit
{
    public class CarColor : MonoBehaviour
    {
        public bool useRandom;

        void Awake()
        {
            if (TryGetComponent<Renderer>(out var renderer))
            {
                if (useRandom)
                    renderer.material.color = new Color(Random.value, Random.value, Random.value);
                else
                    renderer.material.color = CarColorPickerGUI.SelectedCarColor;
            }
        }
    }
}