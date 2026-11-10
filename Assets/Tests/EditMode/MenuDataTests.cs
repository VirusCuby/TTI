using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Pruebas de los datos que usa el menú principal: el catálogo de retos y los ajustes guardados.
/// </summary>
public class MenuDataTests
{
    [Test]
    public void CatalogoDeRetos_TieneOnceRetosConTituloYDescripcion()
    {
        var catalogo = ChallengeManager.Catalog();

        Assert.AreEqual(11, catalogo.Count);
        foreach (var reto in catalogo)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(reto.Key), "reto sin título");
            Assert.IsFalse(string.IsNullOrWhiteSpace(reto.Value), "reto sin descripción: " + reto.Key);
        }
    }

    [Test]
    public void ProgresoDeRetos_SeCuentaYSeReinicia()
    {
        int total = ChallengeManager.Catalog().Count;

        // Respaldo del progreso real del Editor, para devolverlo al terminar la prueba.
        var respaldo = new bool[total];
        for (int i = 0; i < total; i++) respaldo[i] = ChallengeManager.IsDone(i);

        try
        {
            ChallengeManager.ResetAllProgress(total);
            Assert.AreEqual(0, ChallengeManager.CompletedTotal(total));

            PlayerPrefs.SetInt("reto_completado_0", 1);
            PlayerPrefs.SetInt("reto_completado_3", 1);
            Assert.AreEqual(2, ChallengeManager.CompletedTotal(total));
            Assert.IsTrue(ChallengeManager.IsDone(3));
            Assert.IsFalse(ChallengeManager.IsDone(1));

            ChallengeManager.ResetAllProgress(total);
            Assert.AreEqual(0, ChallengeManager.CompletedTotal(total));
        }
        finally
        {
            for (int i = 0; i < total; i++)
                if (respaldo[i]) PlayerPrefs.SetInt("reto_completado_" + i, 1);
            PlayerPrefs.Save();
        }
    }
}
