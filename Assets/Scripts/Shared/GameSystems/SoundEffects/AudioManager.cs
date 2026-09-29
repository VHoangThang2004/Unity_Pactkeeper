using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // Tạo Singleton để có thể gọi từ bất kì script nào
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    private void Awake()
    {
        // Kiểm tra xem đã có AudioManager nào tồn tại chưa
        if (Instance == null)
        {
            Instance = this;
            // Giữ cho GameObject này không bị xóa khi đổi Scene
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Nếu chuyển scene mà bị duplicate thì xóa cái mới
            Destroy(gameObject);
        }
    }

    // Hàm gọi nhạc nền BGM
    public void PlayBGM(AudioClip bgmClip)
    {
        if (bgmClip == null) return;

        // Tránh tình trạng bài nhạc đang chạy bị phát lại từ đầu
        if (musicSource.clip == bgmClip && musicSource.isPlaying) return;

        musicSource.clip = bgmClip;
        musicSource.Play();
    }

    // Hàm gọi tiếng động SFX
    public void PlaySFX(AudioClip sfxClip)
    {
        if (sfxClip == null) return;

        // Dùng PlayOneShot để nhiều tiếng có thể đè lên nhau mà không bị ngắt
        sfxSource.PlayOneShot(sfxClip);
    }
}
