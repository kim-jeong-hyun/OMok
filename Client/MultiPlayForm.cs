using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Client
{
    public partial class MultiPlayForm : Form
    {
        //서버와 연동을 위해 추가
        private Thread thread; //통신을 위한 쓰레드
        private TcpClient tcpClient; //TCP 클라이언트
        private NetworkStream stream;

        private const int rectSize = 33; //오목판 셀 크기
        private const int edgeCount = 15; //오목판 선 개수

        private enum Horse {none = 0, BLACK, WHITE };
        //private Horse[, ] board = new Horse[edgeCount, edgeCount];
        //private Horse nowPlayer = Horse.BLACK;
        //private bool playing = false;

        //서버와 연동을 위해 추가
        private Horse[,] board;
        private Horse nowPlayer;
        private bool nowTurn;
        private bool playing;
        private bool entered;
        private bool threading;

        private bool judge(Horse Player) //승리판정함수
        {
            for (int i = 0; i < edgeCount - 4; i++) //가로  11,* ~ 15,* 까지 가능
                for (int j = 0; j < edgeCount; j++)
                    if (board[i, j] == Player && board[i + 1, j] == Player && board[i + 2, j] == Player &&
                       board[i + 3, j] == Player && board[i + 4, j] == Player)
                        return true; //승리
            for (int i = 0; i < edgeCount; i++) //세로  *,11 ~ *,15까지 가능
                for (int j = 0; j < edgeCount - 4; j++)
                    if (board[i, j] == Player && board[i, j + 1] == Player && board[i, j + 2] == Player && board[i, j + 3] == Player &&
                        board[i, j + 4] == Player)
                        return true;
            for (int i = 0; i < edgeCount - 4; i++) //Y=X 직선
                for (int j = 0; j < edgeCount - 4; j++)
                    if (board[i, j] == Player && board[i + 1, j + 1] == Player && board[i + 2, j + 2] == Player &&
                        board[i + 3, j + 3] == Player && board[i + 4, j + 4] == Player)
                        return true;
            for (int i = 4; i < edgeCount; i++)
                for (int j = 0; j < edgeCount - 4; j++)
                    if (board[i, j] == Player && board[i - 1, j + 1] == Player && board[i - 2, j + 2] == Player &&
                        board[i - 3, j + 3] == Player && board[i - 4, j + 4] == Player)
                        return true;
            return false;
        }
        //게임 다시 초기화
        private void refresh()
        {
            this.boardPicture.Refresh();
            for (int i = 0; i < edgeCount; i++)
            {
                for (int j = 0; j < edgeCount; j++)
                {
                    board[i, j] = Horse.none;
                }
            }
            playButton.Enabled = false;
        }

        public MultiPlayForm()
        {
            InitializeComponent();
            this.playButton.Enabled = false; //맨처음에 게임시작버튼 비활성화시킴
            playing = false;
            entered = false;
            threading = false;
            board = new Horse[edgeCount, edgeCount];
            nowTurn = false;
        }
        private void boardPicture_MouseDown(object sender, MouseEventArgs e)
        {
            if (!playing)
            {
                MessageBox.Show("게임을 시작해주세요");
                return;
            }
            Graphics g = this.boardPicture.CreateGraphics(); //그림 그리기 위해 그래픽스 객체 만들기
            int x = e.X / rectSize; //몇 번째 셀 선택했는지 
            int y = e.Y / rectSize;
            //모든 셀의 위치는 0- 14
            if (x < 0 || y < 0 || x >= edgeCount || y >= edgeCount)
            {
                MessageBox.Show("테두리 벗어날 수 없음");
                return;
            }
            //MessageBox.Show(x + ", " + y);
            if (board[x, y] != Horse.none) return; //놓으려는 곳에 아무것도 없어야 한다.
            board[x, y] = nowPlayer;
            if (nowPlayer == Horse.BLACK)
            {
                SolidBrush brush = new SolidBrush(Color.Black);
                g.FillEllipse(brush, x * rectSize, y * rectSize, rectSize, rectSize);
            }
            else
            {
                SolidBrush brush = new SolidBrush(Color.White);
                g.FillEllipse(brush, x * rectSize, y * rectSize, rectSize, rectSize);
            }
            /*놓은 바둑돌의 위치 보내기*/
            string message = "[Put]" + roomTextBox.Text + "," + x + "," + y;
            byte[] buf = Encoding.ASCII.GetBytes(message);
            stream.Write(buf, 0, buf.Length);
            if (judge(nowPlayer))
            {
                status.Text = "승리 했습니다";
                playing = false;
                playButton.Text = "재시작";
                playButton.Enabled = true;
                return;
            }
            else //오목이 만들어지지 않은 상태라면
            {
                status.Text = "상대방이 둘 차례입니다.";
            }
            nowTurn = false;
        }

            //오목판이 처음에 어떻게 구성되는지 정의
        private void boardPicture_Paint(object sender, PaintEventArgs e)
        {
            Graphics gp = e.Graphics;
            Color lineColor = Color.Black;
            Pen p = new Pen(lineColor, 2);
            //오목판 테두리 그리기
            gp.DrawLine(p, rectSize / 2, rectSize / 2, rectSize / 2, rectSize * edgeCount - rectSize / 2); //좌측
            gp.DrawLine(p, rectSize / 2, rectSize / 2, rectSize * edgeCount - rectSize / 2, rectSize / 2);
            gp.DrawLine(p, rectSize / 2, rectSize * edgeCount - rectSize / 2, rectSize * edgeCount - rectSize / 2, rectSize * edgeCount - rectSize / 2);
            gp.DrawLine(p, rectSize * edgeCount - rectSize / 2, rectSize / 2, rectSize * edgeCount - rectSize / 2, rectSize * edgeCount - rectSize / 2);

            p = new Pen(lineColor, 1);
            //십자가 그리기
            for (int i = rectSize + rectSize / 2; i < rectSize * edgeCount - rectSize / 2; i += rectSize)
            {
                gp.DrawLine(p, rectSize / 2, i, rectSize * edgeCount - rectSize / 2, i);
                gp.DrawLine(p, i, rectSize / 2, i, rectSize * edgeCount - rectSize / 2);
            }
        }

        //클릭했을 때 사용자가 입력한 해당방에 접속하기
        private void enterButton_Click(object sender, EventArgs e)
        {
            tcpClient = new TcpClient();
            tcpClient.Connect("127.0.0.1", 9876);
            stream = tcpClient.GetStream();

            thread = new Thread(new ThreadStart(read));
            thread.Start();
            threading = true;

            //방 접속 진행하기
            string message = "[Enter]";
            byte[] buf = Encoding.ASCII.GetBytes(message + this.roomTextBox.Text);
            stream.Write(buf, 0, buf.Length);

//             this.enterButton.Enabled = false;
//             this.playButton.Enabled = true;
//             this.status.Text = "[" + this.roomTextBox.Text + "]번 방에 접속했습니다.";
        }

        private void read()
        {
            while (true)
            {
                byte[] buf = new byte[1024];
                int bufBytes = stream.Read(buf, 0, buf.Length);
                string message = Encoding.ASCII.GetString(buf, 0, bufBytes);
                /*접속 성공 메시지*/
                if(message.Contains("[Enter]"))
                {
                    this.status.Text = "[" + this.roomTextBox.Text + "]번 방에 접속했습니다";
                    this.roomTextBox.Enabled = false;
                    this.enterButton.Enabled = false;
                    entered = true;
                }
                /*방 꽉찬 경우*/
                if (message.Contains("[Full]"))
                {
                    this.status.Text = "이미 가득 찬 방입니다.";
                    closeNetwork();
                }
                /*게임 시작*/
                if(message.Contains("[Play]"))
                {
                    refresh();
                    string horse = message.Split(']')[1];
                    if(horse.Contains("Black"))
                    {
                        this.status.Text = "당신의 차례입니다";
                        nowTurn = true;
                        nowPlayer = Horse.BLACK; 
                    } else
                    {
                        this.status.Text = "상대방 차례입니다";
                        nowTurn = false;
                        nowPlayer = Horse.WHITE;
                    }
                    playing = true;
                }
                /*상대방이 나간 경우*/
                if(message.Contains("[Exit]"))
                {
                    this.status.Text = "상대방이 나갔습니다";
                    refresh();
                }
                /*상대방이 돌을 둔 경우*/
                if(message.Contains("[Put]"))
                {
                    string position = message.Split(']')[1];
                    int x = Convert.ToInt32(position.Split(',')[0]);
                    int y = Convert.ToInt32(position.Split(',')[1]);
                    Horse enemyPlayer = Horse.none;
                    if(enemyPlayer == Horse.BLACK)
                    {
                        enemyPlayer = Horse.WHITE;
                    } else
                    {
                        enemyPlayer = Horse.BLACK;
                    }
                    if (board[x, y] != Horse.none) continue;
                    board[x, y] = enemyPlayer;
                    Graphics g = this.boardPicture.CreateGraphics();
                    if(enemyPlayer == Horse.BLACK)
                    {
                        SolidBrush brush = new SolidBrush(Color.Black);
                        g.FillEllipse(brush, x * rectSize, y * rectSize, rectSize, rectSize);
                    } else
                    {
                        SolidBrush brush = new SolidBrush(Color.White);
                        g.FillEllipse(brush, x * rectSize, y * rectSize, rectSize, rectSize);
                    }
                    if(judge(enemyPlayer))
                    {
                        status.Text = "패배했습니다";
                        playing = false;
                        playButton.Text = "재시작";
                        playButton.Enabled = true;
                    } else
                    {
                        status.Text = "당신이 둘 차례입니다.";
                    }
                    nowTurn = true;
                }
            }

        }

        private void closeNetwork()
        {
            if(threading && thread.IsAlive)
            {
                thread.Abort();
            }
            if(entered)
            {
                tcpClient.Close();
            }
        }

        private void playButton_Click(object sender, EventArgs e)
        {
            if (!playing)
            {
                refresh();
                playing = true;
                string message = "[Play]";
                byte[] buf = Encoding.ASCII.GetBytes(message + this.roomTextBox.Text);
                stream.Write(buf, 0, buf.Length);
                this.status.Text = "상대 플레이어의 준비를 기다립니다.";
                this.playButton.Enabled = false;
            } 
        }

        private void MultiPlayForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            closeNetwork();
        }
    }
}
