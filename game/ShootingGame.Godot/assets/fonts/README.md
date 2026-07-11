# NotoColorEmoji.subset.ttf

[Noto Color Emoji](https://github.com/googlefonts/noto-emoji)(OFL 1.1、OFL.txt 参照)を
ShootingGame で使う絵文字だけにサブセット化したもの(10.7MB → 約21KB)。

収録: 🚀 👾 🛸 🦑 🐙 ⭐ 🔴 🔹 💥 ❤(+ U+FE0F)

## 再生成手順(絵文字を追加するとき)

Python の fonttools が必要:

```
pip install fonttools
curl -sLO https://github.com/googlefonts/noto-emoji/raw/main/fonts/NotoColorEmoji.ttf
pyftsubset NotoColorEmoji.ttf \
  "--unicodes=U+1F680,U+1F47E,U+1F6F8,U+1F991,U+1F419,U+2B50,U+1F534,U+1F539,U+1F4A5,U+2764,U+FE0F" \
  --output-file=NotoColorEmoji.subset.ttf
rm NotoColorEmoji.ttf
```

フル ttf(10.7MB)をリポジトリに入れない理由: 使う絵文字が数個で、
差分の 99% が二度と参照されないバイナリになるため。
