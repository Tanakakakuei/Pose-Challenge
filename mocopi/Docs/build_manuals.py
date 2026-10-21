from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph, Table, TableStyle
from reportlab.lib.styles import ParagraphStyle
from xml.sax.saxutils import escape

pdfmetrics.registerFont(TTFont('JP',r'C:\Windows\Fonts\BIZ-UDGothicR.ttc',subfontIndex=0))
pdfmetrics.registerFont(TTFont('JPB',r'C:\Windows\Fonts\BIZ-UDGothicB.ttc',subfontIndex=0))
pdfmetrics.registerFontFamily('JP',normal='JP',bold='JPB')
W,H=A4
INK=colors.HexColor('#153345'); TEAL=colors.HexColor('#007E87'); MUTED=colors.HexColor('#526674'); PALE=colors.HexColor('#EAF5F4'); LINE=colors.HexColor('#C8DCDD')
STYLE=ParagraphStyle('body',fontName='JP',fontSize=11,leading=18,textColor=INK,wordWrap='CJK')
SMALL=ParagraphStyle('small',parent=STYLE,fontSize=9,leading=14)
BOLD=ParagraphStyle('bold',parent=STYLE,fontName='JPB',fontSize=12,leading=19)
OUT=Path('output/pdf'); OUT.mkdir(parents=True,exist_ok=True)

class Manual:
 def __init__(self,file,title,short):
  self.c=canvas.Canvas(str(OUT/file),pagesize=A4); self.title=title;self.short=short; self.num=0
  self.c.setTitle(title+' 取扱説明書'); self.c.setAuthor('Pose Challenge'); self.c.setSubject('プログラムの知識がなくても使える操作ガイド')
 def page(self,kicker,title,subtitle):
  if self.num:self.c.showPage()
  self.num+=1;c=self.c
  c.setFillColor(TEAL);c.rect(0,H-14,W,14,fill=1,stroke=0)
  c.setFont('JPB',10);c.drawString(42,H-42,'POSE CHALLENGE  /  '+self.short)
  c.setFillColor(MUTED);c.setFont('JP',9);c.drawRightString(W-42,H-42,'取扱説明書  •  2026.10.20')
  c.setFillColor(TEAL);c.setFont('JPB',10);c.drawString(42,H-78,kicker)
  c.setFillColor(INK);c.setFont('JPB',25);c.drawString(42,H-114,title)
  self.y=H-138;self.p(subtitle,style=SMALL,gap=22)
  c.setStrokeColor(LINE);c.line(42,40,W-42,40)
  c.setFont('JP',8);c.setFillColor(MUTED);c.drawString(42,26,self.title+'  /  操作ガイド');c.drawRightString(W-42,26,f'{self.num:02d} / 04')
 def p(self,text,style=STYLE,gap=12,x=42,width=None):
  p=Paragraph(text,style);w,h=p.wrap(width or W-84,700)
  assert self.y-h>58,('overflow',self.title,self.num,text,self.y,h)
  p.drawOn(self.c,x,self.y-h);self.y-=h+gap
 def section(self,title):self.p(title,BOLD,gap=8)
 def step(self,n,title,body):
  c=self.c;c.setFillColor(TEAL);c.roundRect(42,self.y-24,26,26,6,fill=1,stroke=0);c.setFillColor(colors.white);c.setFont('JPB',13);c.drawCentredString(55,self.y-17,str(n))
  self.p('<b>'+title+'</b><br/>'+body,x=80,width=W-122,gap=18)
 def note(self,title,text):
  p=Paragraph('<b>'+title+'</b><br/>'+text,STYLE);_,h=p.wrap(W-112,600)
  assert self.y-h-28>58
  self.c.setFillColor(PALE);self.c.roundRect(42,self.y-h-24,W-84,h+24,8,fill=1,stroke=0);p.drawOn(self.c,56,self.y-h-12);self.y-=h+39
 def table(self,rows,widths):
  data=[[Paragraph(escape(str(v)).replace('\n','<br/>'),SMALL if i else BOLD) for v in row] for i,row in enumerate(rows)]
  t=Table(data,colWidths=widths,hAlign='LEFT');t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),PALE),('VALIGN',(0,0),(-1,-1),'TOP'),('LINEBELOW',(0,0),(-1,-1),.5,LINE),('LEFTPADDING',(0,0),(-1,-1),10),('RIGHTPADDING',(0,0),(-1,-1),10),('TOPPADDING',(0,0),(-1,-1),10),('BOTTOMPADDING',(0,0),(-1,-1),10)]))
  _,h=t.wrap(W-84,700);assert self.y-h>58,('table overflow',self.num,self.y,h)
  t.drawOn(self.c,42,self.y-h);self.y-=h+20
 def finish(self):self.c.save()

m=Manual('GameMode_Guide_JA.pdf','GameMode','GAME MODE')
m.page('01  はじめて遊ぶ','壁の穴にポーズを合わせる','GameModeは、近づいてくる壁の穴に体の形を合わせるゲームです。まずはマウスだけでも画面の流れを確認できます。')
m.step(1,'UnityでGameModeを開く','Unity Hubからmocopiを開きます。Unity下部のProject欄で「GameMode」を検索し、GameModeのシーンをダブルクリックします。バックアップ名の付いたシーンは選びません。')
m.step(2,'画面上部の ▶ を押す','Unity上部中央の再生ボタンを押し、Gameタブを選びます。現在のシーンはマウス確認用の設定なので、アバター確認から始まります。mocopiで遊ぶ準備は3ページです。')
m.step(3,'人数とアバターを確認する','「1人 / 2人 切り替え」で人数を選び、「このアバターで進む」を押します。2人の場合は次の画面で「協力」または「対戦」を選びます。')
m.step(4,'準備してスタート','マウス確認では「スタート」をクリック。mocopi使用時は「準備 OK」に触れるか、頭の上で両手を近づけたポーズを約2秒保ちます。2人とも準備ができると開始します。')
m.note('5秒のカウントダウン','5 → 4 → 3 → 2 → 1 →「スタート！」のあとに壁が動き始めます。画面操作に慣れてから実際の体の動きで試してください。')
m.page('02  プレイ中の操作','10枚の壁にチャレンジ','近づく壁の穴を見て、腕・脚・体の向きを合わせます。両足が安定する範囲で、無理のないポーズを取りましょう。')
m.section('得点の見方')
m.p('最初は10点です。1枚の壁で触れたCubeが<b>3個以内なら成功</b>、<b>4個以上なら1点減点</b>です。同じCubeに繰り返し触れても、その壁では1個として数えます。10枚が通過すると結果が出ます。')
m.table([['画面の表示・モード','意味'],['POINTS','残っている得点。10点満点です。'],['WALLS / 進行バー','通過し終わった壁の数。10枚で終了します。'],['CLEAR! / KEEP GOING','直前の壁が成功 / 失敗だったことを示します。'],['協力 / 対戦','協力は2人の合計点、対戦は各プレイヤーの得点で結果を見ます。']],[160,W-244])
m.section('ボタンは手でもマウスでも操作できます')
m.p('手をボタンに重ね、短い進行表示がたまるまで保ちます。選択後はいったん手を離してから、次のボタンへ触れてください。画面が切り替わった直後の手の重なりは、誤操作を防ぐため受け付けません。')
m.p('決定や選択には短い決定音、選び直し・戻る操作にはキャンセル音が鳴ります。左右の画面で同じ音量になります。')
m.note('結果が出たら','「もう一度！」で再挑戦。「選択に戻る」で人数や遊び方を選び直せます。終了するときはUnity上部の ▶ をもう一度押します。')
m.page('03  運営・準備する人へ','mocopiを接続する','このページの設定は、参加者が来る前に担当者が行います。コードを開いたり、書き換えたりする必要はありません。')
m.step(1,'PCと送信端末を接続する','PCとmocopiを送る端末を同じネットワークに接続します。mocopi側の装着・姿勢合わせを済ませ、送信先をこのPCのIPv4アドレスに設定します。')
m.table([['プレイヤー','送信先ポート'],['1P','UDP 12351'],['2P','UDP 12352（1Pとは別の送信端末）']],[180,W-264])
m.step(2,'マウス確認用の設定を解除する','Unityの再生を停止します。Hierarchy欄で「GameLoopController」を選び、右側のInspectorにある「Mocopi Local Game Loop Controller」→「Debug Mode」→「Debug Mode On」のチェックを外します。')
m.step(3,'再生して接続状態を確認する','▶ を押し、mocopi側の送信を開始します。アバターが体の動きに追従することを確認してください。接続画面では「接続を確認」を押せます。1人時は頭の上で両手を近づけて約1.5秒保つ方法もあります。')
m.note('本番前に1ラウンド試す','ボタンに手が届くこと、床と足の高さが大きくずれていないこと、2人時はそれぞれのアバターが動くこと、効果音が聞こえることを確認します。足元と周囲に十分な空間を確保してください。')
m.page('04  困ったとき','確認する場所と対処','うまく動かないときは、以下の順に確認してください。保存したポーズ型を使う方法はCaptureModeの説明書にまとめています。')
m.table([['困りごと','確認・対処'],['体を動かしても\nアバターが動かない','mocopiの送信、PCのIPv4アドレス、1P / 2Pのポート、同じネットワークかを確認します。GameLoopControllerのDebug Mode Onも外します。'],['手でボタンを\n押せない','いったん手を離し、再び重ねて少し待ちます。急ぐ場合はマウスでクリックできます。'],['2人用の画面が\n出ない','アバター確認画面で「1人 / 2人 切り替え」を押します。2人分の送信とポート設定を確認します。'],['音が聞こえない','Windowsの出力先・音量を確認し、UnityのGameビューにあるMute Audioが有効なら解除します。'],['作った型が出ない','CaptureModeで保存できたか確認します。ゲームの途中で追加した場合は再挑戦します。保存場所が同じである必要があります。'],['Unity Hubから\n開くと落ちる','このPCではWindowsのフォルダー保護による書き込み拒否が発生しました。再発時はUnity.exeの許可状態を担当者に確認してください。']],[145,W-229])
m.section('開くシーンの場所')
m.p('Project欄を順に開く場合：<br/><b>Assets → MocopiReceiver → Samples → ReceiverSample → Scenes → GameMode</b>',gap=20)
m.note('マウス確認に戻すには','再生を止め、3ページの「Debug Mode On」にチェックを入れます。マウス確認は画面操作と接触のテスト用です。実際の全身ポーズの代わりにはなりません。')
m.finish()

m=Manual('CaptureMode_Guide_JA.pdf','CaptureMode','CAPTURE MODE')
m.page('01  はじめて撮影する','自分のポーズを壁の穴にする','CaptureModeで撮るのは、画面に表示されているアバターの輪郭です。写真や動画ではなく、ゲームに出題する「壁と穴の形」を保存します。')
m.step(1,'撮影用シーンを開く','Unityの再生を停止し、Project欄で「CaptureMaskDataMode」を検索します。同名のシーンをダブルクリックして ▶ を押します。この説明書では、このシーンをCaptureModeと呼びます。')
m.step(2,'アバターと壁を確認する','mocopiを使う場合は、このPCのIPv4アドレス・UDP 12351へ送信します。未接続でも表示中のポーズを撮れます。必要なら「現在位置・カメラ方向に壁を配置」または P キーを押します。')
m.step(3,'「撮影」またはSpaceキーを押す','3秒のカウントダウン中にポーズを取り、最後まで保ちます。撮影した形は、正面の無地の壁の右隣に表示されます。')
m.step(4,'形を整えて保存する','余白を調整し、必要に応じてマスを手直しします。「保存」または S キーで保存します。次のポーズは「撮り直す」または R キーから始めます。')
m.note('撮影が終わったら体を動かして大丈夫','撮影結果は固定されます。その後に体を動かしたりmocopiが切断したりしても、撮った形は変わりません。まずは20種類を目安に作ると、出題に変化が出ます。')
m.page('02  型を整える','穴を広げる・狭める','撮影直後の余白は0cmです。画面左側で全体の余白、右側の「型の手直し」で一部分を調整できます。')
m.section('全体を少し通りやすくする')
m.p('「輪郭から外側への余白」スライダーを右へ動かすと、穴が広がります。調整範囲は<b>0〜30cm</b>です。撮り直しではないので、元のポーズはそのままです。')
m.table([['操作','形の変化'],['Cube追加（穴を狭く）','穴のマスを壁に戻します。隙間を小さくしたいときに使います。'],['Cube削除（穴を広く）','壁のマスを穴にします。通れる場所を増やしたいときに使います。'],['白いマス / 紺色のマス','白 = 壁のCube、紺 = 通れる穴です。1マスは5cmです。']],[185,W-269])
m.section('クリックとドラッグ')
m.p('右側のグリッド、または3Dの撮影結果を操作できます。<b>左クリック</b>は選んだツール、<b>右クリック</b>は常にCube削除です。押したまま動かすと連続して編集できます。')
m.section('失敗したら戻せます')
m.p('「元に戻す」または<b>Ctrl+Z</b>で取り消し、「やり直す」または<b>Ctrl+Y</b>で復元します。ドラッグ1回を1操作として、最大50操作まで戻せます。')
m.note('余白と手直しの関係','手直ししたマスは、余白スライダーを動かしても残ります。「手直しを解除」で現在の余白に基づく形へ戻せます。この解除も「元に戻す」で取り消せます。')
m.page('03  保存とゲームでの利用','保存した型を出題する','保存ボタンを押したときの最終形が、そのままゲーム用のデータになります。ファイルの中身を編集する必要はありません。')
m.step(1,'保存の完了を確認する','「保存」または S キーを押し、画面の保存先表示と「保存済み」の個数を確認します。未変更の同じ形は二重保存できません。保存後に形を変えると、別のデータとして追加保存できます。')
m.step(2,'GameModeへ切り替える','Unityの再生を停止し、GameModeのシーンを開いて ▶ を押します。ゲーム開始時に保存済みの型から10問を抽選します。各プレイヤーは個別に抽選されます。')
m.p('型が10個以上なら、そのラウンド内では重複しません。10個未満なら全種類を出してから再抽選します。有効な型が0個なら、従来の出題を使います。新しい型は次のラウンドから反映されます。')
m.section('保存先を開く')
m.p('Windowsのエクスプローラー上部のアドレス欄に、次の場所を入力できます。画面に表示される保存先が最も確実です。',gap=8)
m.note('このプロジェクトの標準保存先','%USERPROFILE%\\AppData\\LocalLow\\<br/>Sony Corporation\\mocopi Receiver Plugin for Unity')
m.p('ファイル名は「mocopi_pose_日時_固有ID.json」です。別のPCで使う場合は、そのPCでも一度撮影・保存して保存先を確認し、同じフォルダーへJSONファイルをコピーします。',gap=16)
m.note('保存前に気をつけること','R・P・再撮影は、保存していないプレビューや手直しを破棄します。必要な形は先に保存してください。保存済みのファイルは消えません。')
m.page('04  困ったとき・早見表','このページを手元に','キーボード操作が反応しないときは、先にUnityのGame画面をクリックしてからキーを押してください。画面のボタンでも同じ操作ができます。')
m.table([['キー','できること'],['Space','3秒後に撮影する'],['S','現在の最終形を保存する'],['R','未保存のプレビューを捨てて撮り直す'],['P','現在の位置・カメラ方向に壁を配置し直す'],['Ctrl+Z / Ctrl+Y','手直しを取り消す / 復元する'],['左クリック / 右クリック','選択したツール / Cube削除']],[165,W-249])
m.table([['困りごと','対処'],['保存ボタンを押せない','まず撮影します。撮影待ちの間、変更なしの保存済みの形、穴だけ・壁だけの形は保存できません。'],['穴が思った形と違う','mocopiが動いているか確認し、撮影直前のポーズを保持します。アバターの髪・服なども輪郭に含まれます。余白やマス編集で整えられます。'],['未接続の表示が出る','表示中のポーズは撮影できます。実際の体の動きを撮りたい場合は送信先IP・UDP 12351を確認します。'],['前に保存した形を直したい','この画面では過去の保存データを開いて編集する操作はありません。もう一度撮影して、調整した形を新しく保存します。']],[165,W-249])
m.p('シーンの場所：Assets → MocopiReceiver → Samples → ReceiverSample → Scenes → CaptureMaskDataMode',style=SMALL)
m.finish()
print('Created two 4-page Japanese manuals')
