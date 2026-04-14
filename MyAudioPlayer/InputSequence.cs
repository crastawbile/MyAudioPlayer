using System;
using System.Collections.Generic;
using System.Text;

namespace MyAudioPlayer
{
    //音源データを入力する手順に対応するクラス。
    internal class DiskDataInputSequence {
        //まず、次に入力するCDの情報を入力する。
        //それをもとに、DiskDataとDiskProfileを作成する。
        private DiskProfile? _diskProfile;
        private int _trackCount;
        private Dictionary<int, AudioData?> _audioDatas = new Dictionary<int, AudioData?>();
        private Dictionary<int, AudioProfile?> _audioProfiles=new Dictionary<int, AudioProfile?>();
        public void InputDiskData(string seriesName, string diskName, DateTime releaseDate, bool? isOwned,int trackCount) {
            _diskProfile = new DiskProfile(seriesName, diskName, releaseDate, isOwned);
            _trackCount = trackCount;
            for (int i = 1; i <= trackCount; i++) {
                _audioDatas[i] = null;
                _audioProfiles[i] = null;
            }
            //DiskDataを仮置き。
            DiskData = new DiskData {Profile=_diskProfile };
            //仮置きしたDiskDataをデータベースに登録してDiskDataIDを取得、書き込んで上書き。


        }
        public DiskData? DiskData;


        //次に、各トラックのファイルパスと楽曲名と歌唱者リストと歌唱者タイプ(offvocal、通常、ソロ音源、カバー音源)を入力する。
        //楽曲名に該当するMusicDataが存在しなければ、情報を入力してMusicDataを作成する。
        //MusicProfileを楽曲情報入力画面から取得したデータで作成、Audioリストは空のままデータベースに登録してMusicDataIDを取得、書き込んで上書き。

        //トラックごとにAudioDataを作成して、データベースに登録。
        //その際、DiskDataの各トラック情報とMusicDataの音源所持状況も更新。
    }
}
