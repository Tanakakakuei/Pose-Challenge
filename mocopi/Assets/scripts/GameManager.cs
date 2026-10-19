using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI and flow controller for the mocopi alphabet wall game.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private WallManager wallManager;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text contactsText;
    [SerializeField] private Text nextLetterText;
    [SerializeField] private Text gameOverText;

    private bool isResultShown;

    private void Start()
    {
        if (wallManager == null)
        {
            wallManager = FindAnyObjectByType<WallManager>();
        }

        if (wallManager != null)
        {
            wallManager.OnScoreChanged += UpdateScoreDisplay;
            wallManager.OnContactCountChanged += UpdateContactsDisplay;
            wallManager.OnNextLetterChanged += UpdateNextLetterDisplay;
            wallManager.OnProgressChanged += UpdateProgressDisplay;
            wallManager.OnResult += ShowResult;
        }

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (wallManager != null)
        {
            wallManager.OnScoreChanged -= UpdateScoreDisplay;
            wallManager.OnContactCountChanged -= UpdateContactsDisplay;
            wallManager.OnNextLetterChanged -= UpdateNextLetterDisplay;
            wallManager.OnProgressChanged -= UpdateProgressDisplay;
            wallManager.OnResult -= ShowResult;
        }
    }

    public void AddScore(int points)
    {
        Debug.LogWarning("GameManager.AddScore is kept for compatibility. Score is managed by WallManager.");
    }

    private void RefreshAll()
    {
        if (wallManager == null)
        {
            return;
        }

        UpdateScoreDisplay(wallManager.GetScore());
        UpdateContactsDisplay(wallManager.GetContactCount());
        UpdateNextLetterDisplay(wallManager.GetNextLetter());
        UpdateProgressDisplay(wallManager.GetCompletedWallCount(), wallManager.GetWallCount());
    }

    private void UpdateScoreDisplay(int score)
    {
        if (scoreText != null)
        {
            int total = wallManager != null ? wallManager.GetWallCount() : 0;
            scoreText.text = $"Score: {score}/{total}";
        }
    }

    private void UpdateContactsDisplay(int contacts)
    {
        if (contactsText != null)
        {
            int safe = wallManager != null ? wallManager.GetSafeWallCount() : 0;
            int failed = wallManager != null ? wallManager.GetFailedWallCount() : 0;
            contactsText.text = $"Hits: {contacts}  Safe: {safe}  -1: {failed}";
        }
        else if (scoreText != null && wallManager != null)
        {
            scoreText.text = $"Score: {wallManager.GetScore()}/{wallManager.GetWallCount()}  Hits: {contacts}";
        }
    }

    private void UpdateNextLetterDisplay(char letter)
    {
        if (nextLetterText != null)
        {
            int nextIndex = wallManager != null ? Mathf.Min(wallManager.GetSpawnedWallCount() + 1, wallManager.GetWallCount()) : 1;
            int total = wallManager != null ? wallManager.GetWallCount() : 0;
            nextLetterText.text = wallManager != null && wallManager.UsesSavedMasks
                ? $"Pose  {nextIndex}/{total}"
                : $"Next: {letter}  {nextIndex}/{total}";
        }
    }

    private void UpdateProgressDisplay(int completed, int total)
    {
        if (nextLetterText != null && completed >= total)
        {
            nextLetterText.text = $"Result  {completed}/{total}";
        }
    }

    private void ShowResult(WallManager.ResultData result)
    {
        if (isResultShown)
        {
            return;
        }

        isResultShown = true;

        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(true);
            gameOverText.text =
                $"Result\n" +
                $"Score: {result.Score}/{result.TotalWalls}\n" +
                $"Hits: {result.TotalContacts}\n" +
                $"Safe: {result.SafeWalls}  -1: {result.FailedWalls}";
        }

        Debug.Log("Result shown.");
    }

    public void GameOver()
    {
        Debug.LogWarning("GameManager.GameOver is kept for compatibility. The flow now ends with Result.");
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        isResultShown = false;
        if (gameOverText != null)
        {
            gameOverText.gameObject.SetActive(false);
        }

        if (wallManager != null)
        {
            wallManager.ResetGame();
        }

        RefreshAll();
    }

    public int GetCurrentScore() => wallManager != null ? wallManager.GetScore() : 0;
}
