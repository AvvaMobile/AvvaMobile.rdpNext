# rdpNext SSH testleri için yerel OpenSSH sunucusu. Yalnız testte kullanılır.
FROM alpine:3.20
RUN apk add --no-cache openssh bash vim htop ncurses \
 && ssh-keygen -A \
 && adduser -D -s /bin/bash deneme \
 && echo 'deneme:deneme123' | chpasswd \
 && mkdir -p /home/deneme/.ssh && chmod 700 /home/deneme/.ssh && chown deneme:deneme /home/deneme/.ssh \
 && sed -i 's/^#\?PasswordAuthentication.*/PasswordAuthentication yes/' /etc/ssh/sshd_config \
 && echo 'export LANG=C.UTF-8' >> /home/deneme/.bashrc && echo 'source ~/.bashrc' > /home/deneme/.bash_profile
ENV LANG=C.UTF-8
EXPOSE 22
CMD ["/usr/sbin/sshd", "-D", "-e"]
