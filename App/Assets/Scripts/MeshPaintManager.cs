using UnityEngine;

public class MeshPaintManager : MonoBehaviour
{
    public float decaimiento = 0.1f;

    private void Update()
    {
        var pinturas = FindObjectsOfType<MeshPaintData>();
        foreach (var pintura in pinturas)
        {
            bool necesitaUpdate = false;

            for (int i = 0; i < pintura.intensity.Length; i++)
            {
                if (pintura.intensity[i] > 0f)
                {
                    pintura.intensity[i] -= Time.deltaTime * decaimiento;
                    pintura.intensity[i] = Mathf.Max(pintura.intensity[i], 0f);
                    necesitaUpdate = true;
                }
            }

            if (necesitaUpdate)
            {
                pintura.UpdateColors((float t, int i) =>
                {
                    if (!pintura.wasPainted[i])
                        return new Color(0f, 0f, 0f, 0f);

                    return IntensidadAColor(t);
                });
            }
        }
    }

    private Color IntensidadAColor(float t)
    {
        t = Mathf.Clamp01(t);
        Color color;

        if (t > 0.66f)
        {
            float f = Mathf.InverseLerp(1f, 0.66f, t);
            color = Color.Lerp(Color.red, new Color(1f, 0.65f, 0f), f);
        }
        else if (t > 0.33f)
        {
            float f = Mathf.InverseLerp(0.66f, 0.33f, t);
            color = Color.Lerp(new Color(1f, 0.65f, 0f), Color.green, f);
        }
        else
        {
            float f = Mathf.InverseLerp(0.33f, 0f, t);
            color = Color.Lerp(Color.green, Color.blue, f);
        }

        color.a = 0.25f;
        return color;
    }
}